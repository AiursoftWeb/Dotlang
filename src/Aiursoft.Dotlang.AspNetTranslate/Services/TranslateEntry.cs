using System.Security;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Xml;
using Aiursoft.Canon;
using Aiursoft.Dotlang.AspNetTranslate.Models;
using Aiursoft.Dotlang.Shared;

namespace Aiursoft.Dotlang.AspNetTranslate.Services;

public class TranslateEntry(
    DataAnnotationKeyExtractor dataAnnotationKeyExtractor,
    CSharpKeyExtractor keyExtractor,
    CanonPool canonPool,
    CshtmlLocalizer htmlLocalizer,
    CachedTranslateEngine ollamaTranslate,
    ILogger<TranslateEntry> logger)
{
    private readonly string _sep = Path.DirectorySeparatorChar.ToString();

    private void EnsureCsprojFileExistsAsync(string path, bool force)
    {
        var files = Directory.GetFiles(path, "*.csproj", SearchOption.TopDirectoryOnly);
        if (files.Length == 0)
        {
            logger.LogWarning("No csproj file found in path: {Path}", path);
            if (!force)
            {
                throw new InvalidOperationException($"No csproj file found in path: '{path}'. This might be a mistake. Please change directory to the project root and try again.");
            }
        }
    }

    public async Task StartLocalizeContentInCsHtmlAsync(string path, string[] langs, bool takeAction,
        int concurentRequests)
    {
        EnsureCsprojFileExistsAsync(path, false);
        foreach (var lang in langs)
        {
            path = path.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            logger.LogTrace("Starting localization in CSHTML for language: {Lang}", lang);
            var cshtmls = Directory.GetFileSystemEntries(path, "*.cshtml", SearchOption.AllDirectories);
            foreach (var cshtml in cshtmls)
            {
                var fileName = Path.GetFileName(cshtml);
                if (fileName.Contains("_ViewStart") || fileName.Contains("_ViewImports"))
                    continue;

                logger.LogTrace("Localizing content in CSHTML file: {Cshtml}", cshtml);
                await LocalizeContentInCsHtml(cshtml, lang, takeAction, concurentRequests);
            }
        }
    }

    public async Task StartLocalizeContentInCSharpAsync(string path, string[] langs, bool takeAction,
        int concurrentRequests)
    {
        EnsureCsprojFileExistsAsync(path, false);
        foreach (var lang in langs)
        {
            path = path.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            logger.LogTrace("Starting localization in C# for language: {Lang}", lang);
            var csharpFiles = Directory.GetFileSystemEntries(path, "*.cs", SearchOption.AllDirectories);
            foreach (var csFile in csharpFiles)
            {
                if (csFile.EndsWith(".Designer.cs") ||
                    csFile.Contains($"{_sep}obj{_sep}") ||
                    csFile.Contains($"{_sep}bin{_sep}"))
                {
                    continue;
                }

                logger.LogTrace("Localizing content in C# file: {CsFile}", csFile);
                await LocalizeContentInCSharp(path, csFile, lang, takeAction, concurrentRequests);
            }
        }
    }

    public async Task StartLocalizeDataAnnotationsAsync(string path, string[] langs, bool takeAction,
        int concurrentRequests)
    {
        EnsureCsprojFileExistsAsync(path, false);
        foreach (var lang in langs)
        {
            path = path.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            logger.LogTrace("Starting localization for DataAnnotations in C# for language: {Lang}", lang);
            var modelsPath = Path.Combine(path, "Models");
            var csharpFiles = Directory.GetFileSystemEntries(modelsPath, "*.cs", SearchOption.AllDirectories);
            foreach (var csFile in csharpFiles)
            {
                if (csFile.EndsWith(".Designer.cs") ||
                    csFile.Contains($"{_sep}obj{_sep}") ||
                    csFile.Contains($"{_sep}bin{_sep}"))
                {
                    continue;
                }

                logger.LogTrace("Localizing DataAnnotations in C# file: {CsFile}", csFile);
                await LocalizeContentForDataAnnotationAsync(path, csFile, lang, takeAction, concurrentRequests);
            }
        }
    }

    private List<string> DeduplicateKeys(IEnumerable<string> rawKeys, string filePath)
    {
        var uniqueKeys = new List<string>();
        // Key: lowercased/normalized key for collision check
        // Value: the actual accepted key with original casing
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var k in rawKeys)
        {
            if (string.IsNullOrWhiteSpace(k)) continue;

            if (seen.TryGetValue(k, out var existingKey))
            {
                // If we have seen this key (case-insensitive), check if it's the exact same string
                if (!existingKey.Equals(k, StringComparison.Ordinal))
                {
                    logger.LogWarning("Key conflict detected in {File}: '{New}' ignored in favor of '{Existing}'",
                        filePath, k, existingKey);
                }
                // If it is the exact same string, it's just a normal duplicate reference, ignore silently
                continue;
            }

            seen[k] = k;
            uniqueKeys.Add(k);
        }
        return uniqueKeys;
    }

    private async Task LocalizeContentInCSharp(string projectPath, string csPath, string lang, bool takeAction,
        int concurrentRequests)
    {
        if (!File.Exists(csPath))
        {
            logger.LogWarning("File not found: {CsPath}", csPath);
            return;
        }

        var original = await File.ReadAllTextAsync(csPath);
        if (string.IsNullOrWhiteSpace(original))
        {
            logger.LogWarning("File is empty: {CsPath}", csPath);
            return;
        }

        // 1) Extract keys and deduplicate with "first-wins" logic
        var rawKeys = keyExtractor.ExtractLocalizerKeys(original);
        var keys = DeduplicateKeys(rawKeys, csPath);

        logger.LogTrace("Extracted {Count} keys from {File}: {Keys}", keys.Count, csPath, string.Join(", ", keys));

        // 2) Figure out where the .resx should live
        var relativePath = Path.GetRelativePath(projectPath, csPath);
        var resourcePath = Path.Combine(projectPath, "Resources", relativePath);
        var xmlPath = Path.ChangeExtension(resourcePath, $".{lang}.resx");
        Directory.CreateDirectory(Path.GetDirectoryName(xmlPath)!);
        logger.LogTrace("Resx path: {Resx}", xmlPath);

        // 3) Load what’s already been translated
        var existing = await GetResxContentsAsync(xmlPath);

        // 4) Find keys that aren’t yet in the .resx
        var missingKeys = keys
            .Where(k => !existing.ContainsKey(k))
            .ToList();
        logger.LogTrace("Missing keys: {Count} in {Resx}", missingKeys.Count, xmlPath);

        if (!takeAction || missingKeys.Count == 0)
        {
            logger.LogTrace("No new localization needed for: {File}", csPath);
            // Only save if there was existing content (to avoid creating empty files for non-loc files)
            if (takeAction && existing.Count > 0)
            {
                var xml = GenerateXml(existing);
                await File.WriteAllTextAsync(xmlPath, xml);
            }
            return;
        }

        // 5) Translate each missing key
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
                lock (newPairs)
                {
                    newPairs.Add(new TranslatePair
                    {
                        SourceString = key,
                        TargetString = trimmed
                    });
                }

                logger.LogInformation("Translated: \"{Key}\" → \"{Trans}\"", key, trimmed);
            });
        }

        await canonPool.RunAllTasksInPoolAsync(maxDegreeOfParallelism: concurrentRequests);

        // 6) Merge in the new translations and rewrite the .resx
        foreach (var pair in newPairs)
        {
            existing[pair.SourceString] = pair.TargetString.Trim();
        }

        var finalXml = GenerateXml(existing);
        await File.WriteAllTextAsync(xmlPath, finalXml);
        logger.LogInformation("Wrote resource file: {Resx}", xmlPath);
    }

    private async Task LocalizeContentInCsHtml(string cshtmlPath, string lang, bool takeAction, int concurentRequests)
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

        // 1) extract keys and deduplicate
        var rawKeys = htmlLocalizer.ExtractLocalizerKeys(original);
        var keys = DeduplicateKeys(rawKeys, cshtmlPath);

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
            logger.LogTrace("No new localization needed for: {View}", cshtmlPath);
            // Only save if there was existing content (to avoid creating empty files for non-loc files)
            if (takeAction && existing.Count > 0)
            {
                var xml = GenerateXml(existing);
                await File.WriteAllTextAsync(xmlPath, xml);
            }
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
                lock (newPairs)
                {
                    newPairs.Add(new TranslatePair
                    {
                        SourceString = key,
                        TargetString = trimmed
                    });
                }

                logger.LogInformation("Translated: \"{Key}\" → \"{Trans}\"", key, trimmed);
            });
        }

        await canonPool.RunAllTasksInPoolAsync(maxDegreeOfParallelism: concurentRequests);

        // 6) merge in the new translations and rewrite the .resx
        foreach (var pair in newPairs)
        {
            existing[pair.SourceString] = pair.TargetString.Trim();
        }

        var finalXml = GenerateXml(existing);
        await File.WriteAllTextAsync(xmlPath, finalXml);
        logger.LogInformation("Wrote resource file: {Resx}", xmlPath);
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
        try
        {
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

                    // Store original key, case-sensitive
                    if (!resxContents.ContainsKey(key))
                    {
                        resxContents[key] = value;
                    }
                }
            }
        }
        catch (XmlException)
        {
            return new Dictionary<string, string>();
        }

        return resxContents;
    }

    private static string GenerateXml(IDictionary<string, string> entries)
    {
        if (entries.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        // Sort specifically to make output deterministic, though Dictionary order is not guaranteed
        foreach (var kv in entries.OrderBy(k => k.Key))
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

        var (processed, keys) = htmlLocalizer.Process(original);
        if (takeAction)
        {
            foreach (var key in keys)
            {
                logger.LogInformation("Wrapped key: \"{Key}\" in {View}", key, cshtmlPath);
            }

            await File.WriteAllTextAsync(cshtmlPath, processed);
        }
        else
        {
            logger.LogInformation("No new injection needed for: {View}", cshtmlPath);
        }
    }

    private async Task LocalizeContentForDataAnnotationAsync(string projectPath, string csPath, string lang,
        bool takeAction, int concurrentRequests)
    {
        if (!File.Exists(csPath))
        {
            logger.LogWarning("File not found: {CsPath}", csPath);
            return;
        }

        var original = await File.ReadAllTextAsync(csPath);
        if (string.IsNullOrWhiteSpace(original))
        {
            logger.LogWarning("File is empty: {CsPath}", csPath);
            return;
        }

        // 1) Extract keys and deduplicate
        var rawKeys = dataAnnotationKeyExtractor.ExtractKeys(original);
        var keys = DeduplicateKeys(rawKeys, csPath);

        logger.LogTrace("Extracted {Count} DataAnnotation keys from {File}: {Keys}", keys.Count, csPath,
            string.Join(", ", keys));

        // 2) Figure out where the .resx should live
        var relativePath = Path.GetRelativePath(projectPath, csPath);
        var resourcePath = Path.Combine(projectPath, "Resources", relativePath);
        var xmlPath = Path.ChangeExtension(resourcePath, $".{lang}.resx");
        Directory.CreateDirectory(Path.GetDirectoryName(xmlPath)!);
        logger.LogTrace("Resx path: {Resx}", xmlPath);

        // 3) Load what’s already been translated
        var existing = await GetResxContentsAsync(xmlPath);

        // 4) Find keys that aren’t yet in the .resx
        var missingKeys = keys
            .Where(k => !existing.ContainsKey(k))
            .ToList();
        logger.LogTrace("Missing keys: {Count} in {Resx}", missingKeys.Count, xmlPath);

        if (!takeAction || missingKeys.Count == 0)
        {
            logger.LogTrace("No new localization needed for: {File}", csPath);
            // Only save if there was existing content (to avoid creating empty files for non-loc files)
            if (takeAction && existing.Count > 0)
            {
                var xml = GenerateXml(existing);
                await File.WriteAllTextAsync(xmlPath, xml);
            }
            return;
        }

        // 5) Translate each missing key
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
                lock (newPairs)
                {
                    newPairs.Add(new TranslatePair
                    {
                        SourceString = key,
                        TargetString = trimmed
                    });
                }

                logger.LogInformation("Translated: \"{Key}\" → \"{Trans}\"", key, trimmed);
            });
        }

        await canonPool.RunAllTasksInPoolAsync(maxDegreeOfParallelism: concurrentRequests);

        // 6) Merge in the new translations and rewrite the .resx
        foreach (var pair in newPairs)
        {
            existing[pair.SourceString] = pair.TargetString.Trim();
        }

        var finalXml = GenerateXml(existing);
        await File.WriteAllTextAsync(xmlPath, finalXml);
        logger.LogInformation("Wrote resource file: {Resx}", xmlPath);
    }
}
