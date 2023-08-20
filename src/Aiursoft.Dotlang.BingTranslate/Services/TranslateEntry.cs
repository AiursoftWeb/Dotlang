using Aiursoft.Dotlang.BingTranslate.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.RegularExpressions;

namespace Aiursoft.Dotlang.BingTranslate.Services;

public class TranslateEntry
{
    private readonly TranslateOptions _options;
    private readonly BingTranslator _bingTranslator;
    private readonly DocumentAnalyser _documentAnalyser;
    private readonly ILogger<TranslateEntry> _logger;

    public TranslateEntry(
        IOptions<TranslateOptions> options,
        BingTranslator bingTranslator,
        DocumentAnalyser documentAnalyser,
        ILogger<TranslateEntry> logger)
    {
        _options = options.Value;
        _bingTranslator = bingTranslator;
        _documentAnalyser = documentAnalyser;
        _logger = logger;
    }


    public async Task OnServiceStartedAsync(string path, bool shouldTakeAction)
    {
        _logger.LogInformation("Starting application...");
        var currentDirectory = Directory.GetCurrentDirectory();
        var cshtmls = Directory.GetFileSystemEntries(currentDirectory, "*.cshtml", SearchOption.AllDirectories);
        foreach (var cshtml in cshtmls)
        {
            _logger.LogInformation("Analysing: {Cshtml}", cshtml);
            var fileName = Path.GetFileName(cshtml);
            if (fileName.Contains("_ViewStart") || fileName.Contains("_ViewImports"))
            {
                continue;
            }

            var file = await File.ReadAllTextAsync(cshtml);
            var document = _documentAnalyser.AnalyseFile(file);
            var xmlResources = new List<TranslatePair>();
            _logger.LogInformation("Translating: {Cshtml}", cshtml);
            for (int i = 0; i < document.Count; i++)
            {
                var textPart = document[i];
                if (textPart.StringType != StringType.Tag && textPart.Content.Trim() != string.Empty)
                {
                    if (!textPart.Content.Contains('@') && textPart.StringType == StringType.Text)
                    {
                        // Pure text
                        if (xmlResources.All(t => t.SourceString?.Trim() != textPart.Content.Trim()))
                        {
                            xmlResources.Add(new TranslatePair
                            {
                                SourceString = textPart.Content,
                                TargetString = _bingTranslator.CallTranslate(textPart.Content, _options.TargetLanguage)
                            });
                        }
                        textPart.Content = WrapWithTranslateTag(textPart.Content);
                    }
                    else
                    {
                        // Text with razor.
                        var reg = new Regex(@"Localizer\[""(.*?)\""]", RegexOptions.Compiled);
                        var matched = reg.Matches(textPart.Content);
                        foreach (Match match in matched)
                        {
                            var content = match.Groups[1].Value;
                            if (xmlResources.All(t => t.SourceString?.Trim() != content.Trim()))
                            {
                                xmlResources.Add(new TranslatePair
                                {
                                    SourceString = content,
                                    TargetString = _bingTranslator.CallTranslate(content, _options.TargetLanguage)
                                });
                            }
                        }
                    }
                }
                else if (textPart.StringType == StringType.Tag && textPart.Content.ToLower().Trim().StartsWith("<script"))
                {
                    document[i + 1].StringType = StringType.Tag;
                }
                else if (textPart.StringType == StringType.Tag && textPart.Content.ToLower().Trim().StartsWith("<link"))
                {
                    document[i + 1].StringType = StringType.Tag;
                }
            }
            _logger.LogInformation("Rendering: {Cshtml}", cshtml);
            var translated = RenderCsHtml(document);
            var translatedResources = GenerateXml(xmlResources);


            var xmlPosition = cshtml.Replace("\\Views\\", "\\Resources\\Views\\").Replace(".cshtml", $".{_options.TargetLanguage}.resx");
            Directory.CreateDirectory(new FileInfo(xmlPosition).Directory?.FullName ?? throw new NullReferenceException());
            _logger.LogInformation("Writing: {XmlPosition}", xmlPosition);
            if (!string.IsNullOrWhiteSpace(translatedResources))
            {
                await File.WriteAllTextAsync(xmlPosition, translatedResources);
            }
            await File.WriteAllTextAsync(cshtml.Replace(".cshtml", ".cshtml"), translated);
        }

        var modelsPath = Path.Combine(currentDirectory, "Models");
        if (Directory.Exists(modelsPath))
        {
            var csFiles = Directory.GetFileSystemEntries(modelsPath, "*.cs", SearchOption.AllDirectories);
            foreach (var csFile in csFiles)
            {
                var fileContent = await File.ReadAllTextAsync(csFile);
                var allStrings = GetAllStringsInCs(fileContent);
                var xmlResources = new List<TranslatePair>();
                foreach (var stringInCs in allStrings.Where(stringInCs => xmlResources.All(t => t.SourceString?.Trim() != stringInCs.Trim())))
                {
                    xmlResources.Add(new TranslatePair
                    {
                        SourceString = stringInCs,
                        TargetString = _bingTranslator.CallTranslate(stringInCs, _options.TargetLanguage)
                    });
                }
                var translatedResources = GenerateXml(xmlResources);
                var xmlPosition = csFile.Replace("\\Models\\", "\\Resources\\Models\\").Replace(".cs", $".{_options.TargetLanguage}.resx");
                Directory.CreateDirectory(new FileInfo(xmlPosition).Directory?.FullName ?? throw new NullReferenceException());
                _logger.LogInformation("Writing: {XmlPosition}", xmlPosition);
                if (!string.IsNullOrWhiteSpace(translatedResources))
                {
                    await File.WriteAllTextAsync(xmlPosition, translatedResources);
                }
            }
        }
    }

    private List<string> GetAllStringsInCs(string fileContent)
    {
        var s = new List<string>();
        if (fileContent.Count(t => t == '"') < 2)
        {
            return s;
        }
        while (fileContent.Count(t => t == '"') >= 2)
        {
            fileContent = fileContent[(fileContent.IndexOf('"') + 1)..];
            var newString = fileContent[..fileContent.IndexOf('"')];
            fileContent = fileContent[(fileContent.IndexOf('"') + 1)..];
            s.Add(newString);
        }
        return s;
    }

    public static string RenderCsHtml(List<HTMLPart> parts)
    {
        var cshtml = new StringBuilder();
        foreach (var part in parts)
        {
            cshtml.Append(part);
        }
        return cshtml.ToString();
    }

    /// <summary>
    /// Wrap a string with translate tag for cshtml.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public string WrapWithTranslateTag(string input)
    {
        var toTranslate = input.Trim();
        if (toTranslate.Length == 0)
        {
            return string.Empty;
        }
        var translated = $"@Localizer[\"{toTranslate}\"]";
        return input.Replace(toTranslate, translated);
    }

    /// <summary>
    /// Generate Resource XML file based on translate pair.
    /// </summary>
    /// <param name="sourceDocument"></param>
    /// <returns></returns>
    public static string GenerateXml(List<TranslatePair> sourceDocument)
    {
        if (sourceDocument.Count == 0)
        {
            return string.Empty;
        }

        var generatedItems = new StringBuilder();
        foreach (var item in sourceDocument)
        {
            generatedItems.AppendLine($"<data name=\"{item.SourceString?.Trim()}\" xml:space=\"preserve\"><value>{item.TargetString?.Trim()}</value></data>\r\n");
        }

        var templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "Template.xml");
        var template = File.ReadAllText(templatePath);
        var translated = template.Replace("{{Content}}", generatedItems.ToString());
        return translated;
    }
}
