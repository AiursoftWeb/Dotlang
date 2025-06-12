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
        path = path.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        logger.LogInformation("Starting translation on path {path} for language {lang}", path, lang);
        var cshtmls = Directory.GetFileSystemEntries(path, "*.cshtml", SearchOption.AllDirectories);
        foreach (var cshtml in cshtmls)
        {
            var fileName = Path.GetFileName(cshtml);
            if (fileName.Contains("_ViewStart") || fileName.Contains("_ViewImports"))
                continue;

            logger.LogInformation("Processing file: {Cshtml}", cshtml);
            await ProcessCshtmlFileAsync(cshtml, lang, shouldTakeAction);
        }
    }

    private async Task ProcessCshtmlFileAsync(string cshtmlPath, string lang, bool takeAction)
    {
        var original = await File.ReadAllTextAsync(cshtmlPath);

        // inject Localizer and extract keys
        var (processed, keys) = htmlLocalizer.Process(original);
        logger.LogInformation("Injected Localizer in {View}", cshtmlPath);

        // prepare new translations
        var newPairs = new List<TranslatePair>();
        foreach (var key in keys)
        {
            canonPool.RegisterNewTaskToPool(async () =>
            {
                var translated = await ollamaTranslate.TranslateWordInParagraphAsync(
                    sourceContent: original,
                    word: key,
                    language: lang);

                if (!string.Equals(key, translated, StringComparison.OrdinalIgnoreCase))
                {
                    lock (newPairs)
                    {
                        newPairs.Add(new TranslatePair
                        {
                            SourceString = key,
                            TargetString = translated.Trim()
                        });
                    }
                    logger.LogInformation("Translated: \"{Key}\" → \"{Trans}\"", key, translated);
                }
                else
                {
                    logger.LogWarning("No translation needed for: \"{Key}\" in: \"{View}\"", key, cshtmlPath);
                }
            });
        }

        await canonPool.RunAllTasksInPoolAsync(maxDegreeOfParallelism: 2);

        if (!takeAction || newPairs.Count == 0)
        {
            // even if no new translations, update the view file with injection
            await File.WriteAllTextAsync(cshtmlPath, processed);
            return;
        }

        // determine resx path
        var xmlPath = cshtmlPath
            .Replace($"{_sep}Views{_sep}", $"{_sep}Resources{_sep}Views{_sep}")
            .Replace(".cshtml", $".{lang}.resx");
        Directory.CreateDirectory(Path.GetDirectoryName(xmlPath)!);

        // load existing translations
        var existing = await GetResxContentsAsync(xmlPath);

        // merge: existing and new (new override existing)
        var merged = existing.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        foreach (var pair in newPairs)
        {
            merged[pair.SourceString!.Trim()] = pair.TargetString!.Trim();
        }

        // generate XML from merged
        var xml = GenerateXml(merged);
        await File.WriteAllTextAsync(xmlPath, xml);
        logger.LogInformation("Wrote resource file: {Resx}", xmlPath);

        // write back the processed cshtml
        await File.WriteAllTextAsync(cshtmlPath, processed);
        logger.LogInformation("Updated view file: {View}", cshtmlPath);
    }

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
            sb.AppendLine($"  <data name=\"{safeKey}\" xml:space=\"preserve\">\n    <value>{SecurityElement.Escape(kv.Value)}</value>\n  </data>");
        }

        var templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Template.xml");
        var template = File.ReadAllText(templatePath);
        return template.Replace("{{CONTENT}}", sb.ToString());
    }
}
