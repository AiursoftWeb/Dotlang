using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;
using Aiursoft.Doglang.AspNetTranslate.Models;
using Aiursoft.Dotlang.OllamaTranslate;

namespace Aiursoft.Doglang.AspNetTranslate.Services;


public class TranslateEntry(
    CachedTranslateEngine ollamaTranslate,
    ILogger<TranslateEntry> logger)
{
    public async Task StartTranslateAsync(string path, string[] langs, bool shouldTakeAction)
    {
        foreach (var lang in langs)
        {
            logger.LogInformation("Starting translation for language: {Lang}", lang);
            await StartTranslateAsync(path, lang, shouldTakeAction);
        }
    }

    private async Task StartTranslateAsync(string path, string lang, bool shouldTakeAction)
    {
        // Expand '~' to home directory
        path = path.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        logger.LogInformation("Starting translaton on path {path} for language {lang}", path, lang);
        var cshtmls = Directory.GetFileSystemEntries(path, "*.cshtml", SearchOption.AllDirectories);
        foreach (var cshtml in cshtmls)
        {
            logger.LogInformation("Analysing: {Cshtml}", cshtml);
            var fileName = Path.GetFileName(cshtml);
            if (fileName.Contains("_ViewStart") || fileName.Contains("_ViewImports"))
            {
                continue;
            }

            var file = await File.ReadAllTextAsync(cshtml);
            var document = DocumentAnalyser.AnalyseFile(file);
            var xmlResources = new List<TranslatePair>();
            logger.LogInformation("Translating: {Cshtml} to {Lang}", cshtml, lang);
            for (var i = 0; i < document.Count; i++)
            {
                var textPart = document[i];
                if (textPart.StringType != StringType.Tag && textPart.Content.Trim() != string.Empty)
                {
                    if (!textPart.Content.Contains('@') && textPart.StringType == StringType.Text)
                    {
                        // Pure text
                        logger.LogInformation(@"Translating text: ""{Text}"" in file: ""{File}""", textPart.Content, cshtml);
                        var translated = await ollamaTranslate.TranslateWordInParagraphAsync(
                            sourceContent: file,
                            word: textPart.Content,
                            language: lang);
                        if (translated.Trim() != textPart.Content.Trim() &&
                            xmlResources.All(t => t.SourceString?.Trim() != textPart.Content.Trim()))
                        {
                            xmlResources.Add(new TranslatePair
                            {
                                SourceString = textPart.Content,
                                TargetString = translated
                            });
                            textPart.Content = WrapWithTranslateTag(textPart.Content);
                            logger.LogInformation(@"Translated text: ""{Text}"" to: ""{Translated}"" in file: ""{File}""",
                                textPart.Content, translated, cshtml);
                        }
                    }
                    else
                    {
                        // Text with razor.
                        var reg = new Regex("""Localizer\["(.*?)\"]""", RegexOptions.Compiled);
                        var matched = reg.Matches(textPart.Content);
                        foreach (Match match in matched)
                        {
                            logger.LogInformation(@"Translating razor content: ""{Content}"" in file ""{File}""", match.Groups[1].Value, cshtml);
                            var content = match.Groups[1].Value;
                            var translated = await ollamaTranslate.TranslateWordInParagraphAsync(
                                sourceContent: file,
                                word: content,
                                language: lang);
                            if (translated.Trim() != content.Trim() &&
                                xmlResources.All(t => t.SourceString?.Trim() != content.Trim()))
                            {
                                xmlResources.Add(new TranslatePair
                                {
                                    SourceString = content,
                                    TargetString =translated
                                });
                                logger.LogInformation(@"Translated razor content: ""{Content}"" to: ""{Translated}"" in file: ""{File}""",
                                    content, translated, cshtml);
                            }
                        }
                    }
                }
                else switch (textPart.StringType)
                {
                    case StringType.Tag when textPart.Content.ToLower().Trim().StartsWith("<script"):
                    case StringType.Tag when textPart.Content.ToLower().Trim().StartsWith("<link"):
                        document[i + 1].StringType = StringType.Tag;
                        break;
                    case StringType.Razor:
                    case StringType.Text:
                    case StringType.Tag:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(paramName: nameof(textPart.StringType),
                            message: $"Unknown string type: {textPart.StringType}");
                }
            }
            logger.LogInformation("Rendering: {Cshtml}", cshtml);
            var wrappedCsHtml = RenderCsHtml(document);
            var translatedResources = GenerateXml(xmlResources);

            var xmlPosition = cshtml.Replace(@"\Views\", @"\Resources\Views\").Replace(".cshtml", $".{lang}.resx");
            Directory.CreateDirectory(new FileInfo(xmlPosition).Directory?.FullName ?? throw new NullReferenceException());
            logger.LogInformation("Writing: {XmlPosition}", xmlPosition);
            if (!string.IsNullOrWhiteSpace(translatedResources) && shouldTakeAction)
            {
                await File.WriteAllTextAsync(xmlPosition, translatedResources);
            }

            if (shouldTakeAction)
            {
                await File.WriteAllTextAsync(cshtml, wrappedCsHtml);
            }
        }

        var modelsPath = Path.Combine(path, "Models");
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
                        TargetString = await ollamaTranslate.TranslateWordInParagraphAsync(
                            sourceContent: fileContent,
                            word: stringInCs,
                            language: lang)
                    });
                }
                var translatedResources = GenerateXml(xmlResources);
                var xmlPosition = csFile.Replace(@"\Models\", @"\Resources\Models\").Replace(".cs", $".{lang}.resx");
                Directory.CreateDirectory(new FileInfo(xmlPosition).Directory?.FullName ?? throw new NullReferenceException());
                logger.LogInformation("Writing: {XmlPosition}", xmlPosition);
                if (!string.IsNullOrWhiteSpace(translatedResources))
                {
                    await File.WriteAllTextAsync(xmlPosition, translatedResources);
                }
            }
        }
    }

    private static List<string> GetAllStringsInCs(string fileContent)
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

    private static string RenderCsHtml(List<HtmlPart> parts)
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
    private static string WrapWithTranslateTag(string input)
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
    private static string GenerateXml(List<TranslatePair> sourceDocument)
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
        var translated = template.Replace("{{CONTENT}}", generatedItems.ToString());
        return translated;
    }
}
