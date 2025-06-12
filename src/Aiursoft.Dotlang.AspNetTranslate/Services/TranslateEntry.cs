using System.Security;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Xml;
using Aiursoft.Canon;
using Aiursoft.Dotlang.AspNetTranslate.Models;
using Aiursoft.Dotlang.Shared;

namespace Aiursoft.Dotlang.AspNetTranslate.Services;

public class TranslateEntry(
    CanonPool canonPool,
    CshtmlLocalizer htmlLocalizer,
    CachedTranslateEngine ollamaTranslate,
    ILogger<TranslateEntry> logger)
{
    private readonly string _sep = Path.DirectorySeparatorChar.ToString();

    public async Task StartWrapWithLocalizerAsync(string path, bool takeAction)
    {
        path = path.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        logger.LogInformation("Wrapping with Localizer on path {path}", path);
        var cshtmls = Directory.GetFileSystemEntries(path, "*.cshtml", SearchOption.AllDirectories);
        foreach (var cshtml in cshtmls)
        {
            var fileName = Path.GetFileName(cshtml);
            if (fileName.Contains("_ViewStart") || fileName.Contains("_ViewImports"))
                continue;

            logger.LogInformation("Wrapping file with Localizer: {Cshtml}", cshtml);
            await WrapWithLocalizerAsync(cshtml, takeAction);
        }
    }

    public async Task StartLocalizeContentInCsHtmlAsync(string path, string[] langs, bool takeAction)
    {
        foreach (var lang in langs)
        {
            path = path.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            logger.LogInformation("Starting localization in CSHTML for language: {Lang}", lang);
            var cshtmls = Directory.GetFileSystemEntries(path, "*.cshtml", SearchOption.AllDirectories);
            foreach (var cshtml in cshtmls)
            {
                var fileName = Path.GetFileName(cshtml);
                if (fileName.Contains("_ViewStart") || fileName.Contains("_ViewImports"))
                    continue;

                logger.LogInformation("Localizing content in CSHTML file: {Cshtml}", cshtml);
                await LocalizeContentInCsHtml(cshtml, lang, takeAction);
            }
        }
    }

    private async Task WrapWithLocalizerAsync(string cshtmlPath, bool takeAction)
    {
        if (!File.Exists(cshtmlPath))
        {
            logger.LogWarning("File not found: {CshtmlPath}", cshtmlPath);
            return;
        }

        // Read the original content
        var original = await File.ReadAllTextAsync(cshtmlPath);
        if (string.IsNullOrWhiteSpace(original))
        {
            logger.LogWarning("File is empty: {CshtmlPath}", cshtmlPath);
            return;
        }

        var (processed, _) = htmlLocalizer.Process(original);
        if (takeAction)
        {
            await File.WriteAllTextAsync(cshtmlPath, processed);
        }
        else
        {
            logger.LogInformation("No new injection needed for: {View}", cshtmlPath);
        }
    }

    private async Task LocalizeContentInCsHtml(string cshtmlPath, string lang, bool takeAction)
    {
        if (!File.Exists(cshtmlPath))
        {
            logger.LogWarning("File not found: {CshtmlPath}", cshtmlPath);
            return;
        }

        // Read the original content
        var original = await File.ReadAllTextAsync(cshtmlPath);
        if (string.IsNullOrWhiteSpace(original))
        {
            logger.LogWarning("File is empty: {CshtmlPath}", cshtmlPath);
            return;
        }

        // 1) extract all Localizer-wrapped keys
        var keys = htmlLocalizer.ExtractLocalizerKeys(original)
            .Select(k => k.Trim())
            .Where(k => k.Length > 0)
            .Distinct()
            .ToList();
        logger.LogTrace("Extracted {Count} keys from {View}: {Keys}", keys.Count, cshtmlPath, string.Join(", ", keys));

        // 2) figure out where the .resx should live
        var xmlPath = cshtmlPath
            .Replace($"{_sep}Views{_sep}", $"{_sep}Resources{_sep}Views{_sep}")
            .Replace(".cshtml", $".{lang}.resx");
        Directory.CreateDirectory(Path.GetDirectoryName(xmlPath)!);
        logger.LogTrace("Resx path: {Resx}", xmlPath);

        // 3) load what’s already been translated
        var existing = await GetResxContentsAsync(xmlPath);

        // 4) find keys that aren’t yet in the .resx
        var missingKeys = keys
            .Where(k => !existing.ContainsKey(k))
            .ToList();
        logger.LogTrace("Missing keys: {Count} in {Resx}", missingKeys.Count, xmlPath);

        if (!takeAction || missingKeys.Count == 0)
        {
            logger.LogInformation("No new localization needed for: {View}", cshtmlPath);
            return;
        }

        // 5) translate each missing key
        var newPairs = new List<TranslatePair>();
        foreach (var key in missingKeys)
        {
            logger.LogInformation("Translating: \"{Key}\"", key);
            canonPool.RegisterNewTaskToPool(async () =>
            {
                var translated = await ollamaTranslate.TranslateWordInParagraphAsync(
                    sourceContent: original,
                    word: key,
                    language: lang);
                var trimmed = translated.Trim();
                if (!string.Equals(key, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    lock (newPairs)
                    {
                        newPairs.Add(new TranslatePair
                        {
                            SourceString = key,
                            TargetString = trimmed
                        });
                    }
                    logger.LogInformation("Translated: \"{Key}\" → \"{Trans}\"", key, trimmed);
                }
                else
                {
                    logger.LogWarning("No translation needed for: \"{Key}\"", key);
                }
            });
        }

        await canonPool.RunAllTasksInPoolAsync(maxDegreeOfParallelism: 16);

        // 6) merge in the new translations and rewrite the .resx
        foreach (var pair in newPairs)
        {
            existing[pair.SourceString!.Trim()] = pair.TargetString!.Trim();
        }

        var xml = GenerateXml(existing);
        await File.WriteAllTextAsync(xmlPath, xml);
        logger.LogInformation("Wrote resource file: {Resx}", xmlPath);
    }

    // private async Task ProcessCshtmlFileAsync(string cshtmlPath, string lang, bool takeAction)
    // {
    //     var original = await File.ReadAllTextAsync(cshtmlPath);
    //
    //     // inject Localizer and extract keys
    //     var (processed, keys) = htmlLocalizer.Process(original);
    //     logger.LogInformation("Injected Localizer in {View}", cshtmlPath);
    //
    //     // prepare new translations
    //     var newPairs = new List<TranslatePair>();
    //     foreach (var key in keys)
    //     {
    //         canonPool.RegisterNewTaskToPool(async () =>
    //         {
    //             var translated = await ollamaTranslate.TranslateWordInParagraphAsync(
    //                 sourceContent: original,
    //                 word: key,
    //                 language: lang);
    //
    //             if (!string.Equals(key, translated, StringComparison.OrdinalIgnoreCase))
    //             {
    //                 lock (newPairs)
    //                 {
    //                     newPairs.Add(new TranslatePair
    //                     {
    //                         SourceString = key,
    //                         TargetString = translated.Trim()
    //                     });
    //                 }
    //
    //                 logger.LogInformation("Translated: \"{Key}\" → \"{Trans}\"", key, translated);
    //             }
    //             else
    //             {
    //                 logger.LogWarning("No translation needed for: \"{Key}\" in: \"{View}\"", key, cshtmlPath);
    //             }
    //         });
    //     }
    //
    //     await canonPool.RunAllTasksInPoolAsync(maxDegreeOfParallelism: 2);
    //
    //     if (!takeAction || newPairs.Count == 0)
    //     {
    //         // even if no new translations, update the view file with injection
    //         await File.WriteAllTextAsync(cshtmlPath, processed);
    //         return;
    //     }
    //
    //     // determine resx path
    //     var xmlPath = cshtmlPath
    //         .Replace($"{_sep}Views{_sep}", $"{_sep}Resources{_sep}Views{_sep}")
    //         .Replace(".cshtml", $".{lang}.resx");
    //     Directory.CreateDirectory(Path.GetDirectoryName(xmlPath)!);
    //
    //     // load existing translations
    //     var existing = await GetResxContentsAsync(xmlPath);
    //
    //     // merge: existing and new (new override existing)
    //     var merged = existing.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    //     foreach (var pair in newPairs)
    //     {
    //         merged[pair.SourceString!.Trim()] = pair.TargetString!.Trim();
    //     }
    //
    //     // generate XML from merged
    //     var xml = GenerateXml(merged);
    //     await File.WriteAllTextAsync(xmlPath, xml);
    //     logger.LogInformation("Wrote resource file: {Resx}", xmlPath);
    //
    //     // write back the processed cshtml
    //     await File.WriteAllTextAsync(cshtmlPath, processed);
    //     logger.LogInformation("Updated view file: {View}", cshtmlPath);
    // }

    private async Task<Dictionary<string, string>> GetResxContentsAsync(string path)
    {
        if (path.StartsWith("~"))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            path = Path.Combine(home, path[1..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        if (!File.Exists(path))
            return new Dictionary<string, string>();

        var resxContents = new Dictionary<string, string>();
        var settings = new XmlReaderSettings
        {
            IgnoreComments = true,
            IgnoreWhitespace = true,
            Async = true
        };
        using var reader = XmlReader.Create(path, settings);
        while (await reader.ReadAsync())
        {
            if (reader is { NodeType: XmlNodeType.Element, Name: "data" })
            {
                var key = reader.GetAttribute("name");
                if (string.IsNullOrEmpty(key)) continue;

                var value = string.Empty;
                while (await reader.ReadAsync())
                {
                    if (reader is { NodeType: XmlNodeType.Element, Name: "value" })
                    {
                        value = await reader.ReadElementContentAsStringAsync();
                        break;
                    }

                    if (reader is { NodeType: XmlNodeType.EndElement, Name: "data" })
                        break;
                }

                resxContents[key] = value;
            }
        }

        return resxContents;
    }

    private static string GenerateXml(IDictionary<string, string> entries)
    {
        if (entries.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var kv in entries)
        {
            // change " to &quot; for XML compatibility
            // change < to &lt; and > to &gt; for XML compatibility
            // change & to &amp; for XML compatibility
            // to do that, use SecurityElement.Escape
            var safeKey = SecurityElement.Escape(kv.Key);
            sb.AppendLine(
                $"  <data name=\"{safeKey}\" xml:space=\"preserve\">\n    <value>{SecurityElement.Escape(kv.Value)}</value>\n  </data>");
        }

        var templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Template.xml");
        var template = File.ReadAllText(templatePath);
        return template.Replace("{{CONTENT}}", sb.ToString());
    }
}
