using Microsoft.Extensions.Logging;
using System.Text;
using Aiursoft.Dotlang.AspNetTranslate.Models;
using Aiursoft.Dotlang.Shared;

namespace Aiursoft.Dotlang.AspNetTranslate.Services;

public class TranslateEntry(
    CshtmlLocalizer htmlLocalizer,
    CachedTranslateEngine ollamaTranslate,
    ILogger<TranslateEntry> logger)
{
    // On Windows, should be \
    // On Linux, should be /
    private readonly string _ = Path.DirectorySeparatorChar.ToString();

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

        logger.LogInformation("Starting translation on path {path} for language {lang}", path, lang);
        var cshtmls = Directory.GetFileSystemEntries(path, "*.cshtml", SearchOption.AllDirectories);
        foreach (var cshtml in cshtmls)
        {
            logger.LogInformation("Processing file: {Cshtml}", cshtml);
            var fileName = Path.GetFileName(cshtml);
            if (fileName.Contains("_ViewStart") || fileName.Contains("_ViewImports"))
            {
                continue;
            }
            await ProcessCshtmlFileAsync(cshtml, lang, shouldTakeAction);
        }

        // foreach (var cshtml in cshtmls)
        // {
        //     var xmlPosition = cshtml.Replace(@$"{_}Views{_}", $@"{_}Resources{_}Views{_}")
        //         .Replace(".cshtml", $".{lang}.resx");
        //     var xmlAlreadyExists = File.Exists(xmlPosition);
        //     if (xmlAlreadyExists)
        //     {
        //         logger.LogInformation("Skipping {Cshtml} as XML already exists at {XmlPosition}", cshtml, xmlPosition);
        //         continue;
        //     }
        //     else
        //     {
        //         logger.LogInformation("XML does not exist at {XmlPosition}, creating...", xmlPosition);
        //     }
        //

        //
        //     var file = await File.ReadAllTextAsync(cshtml);
        //     var document = DocumentAnalyser.AnalyseFile(file);
        //     var xmlResources = new List<TranslatePair>();
        //     logger.LogInformation("Translating: {Cshtml} to {Lang}", cshtml, lang);
        //     for (var i = 0; i < document.Count; i++)
        //     {
        //         var textPart = document[i];
        //         if (textPart.StringType != StringType.Tag && textPart.Content.Trim() != string.Empty)
        //         {
        //             if (!textPart.Content.Contains('@') && textPart.StringType == StringType.Text)
        //             {
        //                 // Pure text
        //                 logger.LogInformation(@"Translating text: ""{Text}"" in file: ""{File}""", textPart.Content,
        //                     cshtml);
        //                 var translated = await ollamaTranslate.TranslateWordInParagraphAsync(
        //                     sourceContent: file,
        //                     word: textPart.Content,
        //                     language: lang);
        //                 if (translated.Trim() != textPart.Content.Trim() &&
        //                     xmlResources.All(t => t.SourceString?.Trim() != textPart.Content.Trim()))
        //                 {
        //                     xmlResources.Add(new TranslatePair
        //                     {
        //                         SourceString = textPart.Content,
        //                         TargetString = translated
        //                     });
        //                     textPart.Content = WrapWithTranslateTag(textPart.Content);
        //                     logger.LogInformation(
        //                         @"Translated text: ""{Text}"" to: ""{Translated}"" in file: ""{File}""",
        //                         textPart.Content, translated, cshtml);
        //                 }
        //             }
        //             else
        //             {
        //                 // Text with razor.
        //                 var reg = new Regex("""Localizer\["(.*?)\"]""", RegexOptions.Compiled);
        //                 var matched = reg.Matches(textPart.Content);
        //                 foreach (Match match in matched)
        //                 {
        //                     logger.LogInformation(@"Translating razor content: ""{Content}"" in file ""{File}""",
        //                         match.Groups[1].Value, cshtml);
        //                     var content = match.Groups[1].Value;
        //                     var translated = await ollamaTranslate.TranslateWordInParagraphAsync(
        //                         sourceContent: file,
        //                         word: content,
        //                         language: lang);
        //                     if (translated.Trim() != content.Trim() &&
        //                         xmlResources.All(t => t.SourceString?.Trim() != content.Trim()))
        //                     {
        //                         xmlResources.Add(new TranslatePair
        //                         {
        //                             SourceString = content,
        //                             TargetString = translated
        //                         });
        //                         logger.LogInformation(
        //                             @"Translated razor content: ""{Content}"" to: ""{Translated}"" in file: ""{File}""",
        //                             content, translated, cshtml);
        //                     }
        //                 }
        //             }
        //         }
        //         else
        //             switch (textPart.StringType)
        //             {
        //                 case StringType.Tag when textPart.Content.ToLower().Trim().StartsWith("<script"):
        //                 case StringType.Tag when textPart.Content.ToLower().Trim().StartsWith("<link"):
        //                     document[i + 1].StringType = StringType.Tag;
        //                     break;
        //                 case StringType.Razor:
        //                 case StringType.Text:
        //                 case StringType.Tag:
        //                     break;
        //                 default:
        //                     throw new ArgumentOutOfRangeException(paramName: nameof(textPart.StringType),
        //                         message: $"Unknown string type: {textPart.StringType}");
        //             }
        //     }
        //
        //     logger.LogInformation("Rendering: {Cshtml}", cshtml);
        //     var wrappedCsHtml = RenderCsHtml(document);
        //     var translatedResources = GenerateXml(xmlResources);
        //
        //     Directory.CreateDirectory(new FileInfo(xmlPosition).Directory?.FullName ??
        //                               throw new NullReferenceException());
        //     logger.LogInformation("Writing: {XmlPosition}", xmlPosition);
        //     if (!string.IsNullOrWhiteSpace(translatedResources) && shouldTakeAction)
        //     {
        //         await File.WriteAllTextAsync(xmlPosition, translatedResources);
        //     }
        //
        //     if (shouldTakeAction)
        //     {
        //         await File.WriteAllTextAsync(cshtml, wrappedCsHtml);
        //     }
        // }
        // var modelsPath = Path.Combine(path, "Models");
        // if (Directory.Exists(modelsPath))
        // {
        //     var csFiles = Directory.GetFileSystemEntries(modelsPath, "*.cs", SearchOption.AllDirectories);
        //     foreach (var csFile in csFiles)
        //     {
        //         var fileContent = await File.ReadAllTextAsync(csFile);
        //         var allStrings = GetAllStringsInCs(fileContent);
        //         var xmlResources = new List<TranslatePair>();
        //         foreach (var stringInCs in allStrings.Where(stringInCs =>
        //                      xmlResources.All(t => t.SourceString?.Trim() != stringInCs.Trim())))
        //         {
        //             xmlResources.Add(new TranslatePair
        //             {
        //                 SourceString = stringInCs,
        //                 TargetString = await ollamaTranslate.TranslateWordInParagraphAsync(
        //                     sourceContent: fileContent,
        //                     word: stringInCs,
        //                     language: lang)
        //             });
        //         }
        //
        //         var translatedResources = GenerateXml(xmlResources);
        //         var xmlPosition = csFile.Replace(@$"{_}Models{_}", $@"{_}Resources{_}Models{_}")
        //             .Replace(".cs", $".{lang}.resx");
        //         Directory.CreateDirectory(new FileInfo(xmlPosition).Directory?.FullName ??
        //                                   throw new NullReferenceException());
        //         logger.LogInformation("Writing: {XmlPosition}", xmlPosition);
        //         if (!string.IsNullOrWhiteSpace(translatedResources))
        //         {
        //             await File.WriteAllTextAsync(xmlPosition, translatedResources);
        //         }
        //     }
        // }
    }

       /// <summary>
    /// 处理单个 .cshtml 文件：解析、注入 @Localizer、调用翻译引擎、生成 resx 并写回。
    /// </summary>
    private async Task ProcessCshtmlFileAsync(string cshtmlPath, string lang, bool takeAction)
    {
        // 1. 读取原始 cshtml
        var original = await File.ReadAllTextAsync(cshtmlPath);

        // 2. 用 Razor+Roslyn 精准注入 @Localizer 并收集所有纯文本 keys
        var (processed, keys) = htmlLocalizer.Process(original);
        logger.LogInformation("Injected Localizer in {View}", cshtmlPath);

        // 3. 针对每个 key 调用翻译引擎，构造 TranslatePair 列表
        var pairs = new List<TranslatePair>();
        foreach (var key in keys)
        {
            logger.LogInformation("Translating \"{Key}\" in {View}", key, cshtmlPath);
            var translated = await ollamaTranslate.TranslateWordInParagraphAsync(
                sourceContent: original,
                word: key,
                language: lang);

            if (!string.Equals(key, translated, StringComparison.OrdinalIgnoreCase))
            {
                pairs.Add(new TranslatePair
                {
                    SourceString = key,
                    TargetString = translated.Trim()
                });
                logger.LogInformation(@"Translated: ""{Key}"" → ""{Trans}""", key, translated);
            }
            else
            {
                logger.LogWarning(@"No translation needed for: ""{Key}"" in: ""{View}""", key, cshtmlPath);
            }
        }

        // 4. 生成 .resx 内容
        var xml = GenerateXml(pairs);

        if (!string.IsNullOrWhiteSpace(xml) && takeAction)
        {
            // 5. 写入资源文件
            var xmlPath = cshtmlPath
                .Replace($"{_}Views{_}", $"{_}Resources{_}Views{_}")
                .Replace(".cshtml", $".{lang}.resx");
            Directory.CreateDirectory(Path.GetDirectoryName(xmlPath)!);
            await File.WriteAllTextAsync(xmlPath, xml);
            logger.LogInformation("Wrote resource file: {Resx}", xmlPath);

            // 6. 写回注入了 @Localizer 的 cshtml
            await File.WriteAllTextAsync(cshtmlPath, processed);
            logger.LogInformation("Updated view file: {View}", cshtmlPath);
        }
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
            generatedItems.AppendLine(
                $"<data name=\"{item.SourceString?.Trim()}\" xml:space=\"preserve\"><value>{item.TargetString?.Trim()}</value></data>\r\n");
        }

        var templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Template.xml");
        var template = File.ReadAllText(templatePath);
        var translated = template.Replace("{{CONTENT}}", generatedItems.ToString());
        return translated;
    }
}
