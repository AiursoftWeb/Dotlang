using CoreTranslator.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CoreTranslator.Services
{
    public class TranslatorCore
    {
        private readonly BingTranslator _bingtranslator;
        private readonly DocumentAnalyser _documentAnalyser;
        private readonly ILogger<TranslatorCore> _logger;

        public TranslatorCore(
            BingTranslator bingTranslator,
            DocumentAnalyser documentAnalyser,
            ILoggerFactory loggerFactory)
        {
            _bingtranslator = bingTranslator;
            _documentAnalyser = documentAnalyser;
            _logger = loggerFactory.CreateLogger<TranslatorCore>();
        }

        public void DoWork()
        {
            _logger.LogInformation("Starting application...");
            var currentDirectory = Directory.GetCurrentDirectory();
            string[] cshtmls = Directory.GetFileSystemEntries(currentDirectory, "*.cshtml", SearchOption.AllDirectories);
            foreach (var cshtml in cshtmls)
            {
                _logger.LogInformation($"Analysing: {cshtml}");
                var fileName = Path.GetFileName(cshtml);
                if (fileName.Contains("_ViewStart") || fileName.Contains("_ViewImports"))
                {
                    continue;
                }

                var file = File.ReadAllText(cshtml);
                var document = _documentAnalyser.AnalyseFile(file);
                var xmlResources = new List<TranslatePair>();
                _logger.LogInformation($"Translating: {cshtml}");
                for (int i = 0; i < document.Count; i++)
                {
                    var textPart = document[i];
                    if (textPart.StringType != StringType.Tag && textPart.Content.Trim() != string.Empty)
                    {
                        if (!textPart.Content.Contains('@') && textPart.StringType == StringType.Text)
                        {
                            // Pure text
                            if (!xmlResources.Any(t => t.SourceString?.Trim() == textPart.Content.Trim()))
                            {
                                xmlResources.Add(new TranslatePair
                                {
                                    SourceString = textPart.Content,
                                    TargetString = _bingtranslator.CallTranslate(textPart.Content, "zh")
                                });
                            }
                            textPart.Content = Translate(textPart.Content);
                        }
                        else
                        {
                            // Text with razor.
                            var reg = new Regex(@"Localizer\[""(.*?)\""]", RegexOptions.Compiled);
                            var matched = reg.Matches(textPart.Content);
                            foreach (Match match in matched)
                            {
                                var content = match.Groups[1].Value;
                                if (!xmlResources.Any(t => t.SourceString?.Trim() == content.Trim()))
                                {
                                    xmlResources.Add(new TranslatePair
                                    {
                                        SourceString = content,
                                        TargetString = _bingtranslator.CallTranslate(content, "zh")
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
                _logger.LogInformation($"Rendering: {cshtml}");
                var translated = RenderCSHtml(document);
                var translatedResources = GenerateXML(xmlResources);


                var xmlPosition = cshtml.Replace("\\Views\\", "\\Resources\\Views\\").Replace(".cshtml", ".zh.resx");
                var toWrite = Directory.CreateDirectory(new FileInfo(xmlPosition).Directory?.FullName ?? throw new NullReferenceException());
                _logger.LogInformation($"Writting: {xmlPosition}");
                if (!string.IsNullOrWhiteSpace(translatedResources))
                {
                    File.WriteAllText(xmlPosition, translatedResources);
                }
                File.WriteAllText(cshtml.Replace(".cshtml", ".cshtml"), translated);
            }

            var modelsPath = Path.Combine(currentDirectory, "Models");
            if (Directory.Exists(modelsPath))
            {
                string[] csfiles = Directory.GetFileSystemEntries(modelsPath, "*.cs", SearchOption.AllDirectories);
                foreach (var csfile in csfiles)
                {
                    var fileContent = File.ReadAllText(csfile);
                    var allstrings = GetAllStringsInCS(fileContent);
                    var xmlResources = new List<TranslatePair>();
                    foreach (var stringInCs in allstrings)
                    {
                        if (!xmlResources.Any(t => t.SourceString?.Trim() == stringInCs.Trim()))
                        {
                            xmlResources.Add(new TranslatePair
                            {
                                SourceString = stringInCs,
                                TargetString = _bingtranslator.CallTranslate(stringInCs, "zh")
                            });
                        }
                    }
                    var translatedResources = GenerateXML(xmlResources);
                    var xmlPosition = csfile.Replace("\\Models\\", "\\Resources\\Models\\").Replace(".cs", ".zh.resx");
                    Directory.CreateDirectory(new FileInfo(xmlPosition).Directory?.FullName ?? throw new NullReferenceException());
                    _logger.LogInformation($"Writing: {xmlPosition}");
                    if (!string.IsNullOrWhiteSpace(translatedResources))
                    {
                        File.WriteAllText(xmlPosition, translatedResources);
                    }
                }
            }
        }

        public List<string> GetAllStringsInCS(string fileContent)
        {
            List<string> s = new List<string>();
            if (fileContent.Where(t => t == '"').Count() < 2)
            {
                return s;
            }
            while (fileContent.Where(t => t == '"').Count() >= 2)
            {
                fileContent = fileContent.Substring(fileContent.IndexOf('"') + 1);
                string newString = fileContent.Substring(0, fileContent.IndexOf('"'));
                fileContent = fileContent.Substring(fileContent.IndexOf('"') + 1);
                s.Add(newString);
            }
            return s;
        }

        public string RenderCSHtml(List<HTMLPart> parts)
        {
            string cshtml = "";
            foreach (var part in parts)
            {
                if (part != null)
                {
                    cshtml += part.Content;
                }
            }
            return cshtml;
        }

        public string Translate(string input)
        {
            var toTranslate = input.Trim();
            if (toTranslate.Length == 0)
            {
                return "";
            }
            var translated = $"@Localizer[\"{toTranslate}\"]";
            return input.Replace(toTranslate, translated);
        }

        public string GenerateXML(List<TranslatePair> sourceDocument)
        {
            if (sourceDocument.Count == 0)
            {
                return string.Empty;
            }
            var programPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var generatedItems = string.Empty;
            foreach (var item in sourceDocument)
            {
                generatedItems += $"<data name=\"{item.SourceString?.Trim()}\" xml:space=\"preserve\"><value>{item.TargetString?.Trim()}</value></data>\r\n";
            }
            var template = File.ReadAllText(programPath + "\\Template.xml");
            var translated = template.Replace("{{Content}}", generatedItems);
            return translated;
        }
    }
}
