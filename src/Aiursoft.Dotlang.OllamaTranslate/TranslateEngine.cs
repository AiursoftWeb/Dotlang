using Microsoft.Extensions.Logging;

namespace Aiursoft.Dotlang.OllamaTranslate;

public class TranslateEngine(ILogger<TranslateEngine> logger, OllamaBasedTranslatorEngine ollamaTranslateEngine)
{
    public async Task TranslateAsync(
        string sourceFolder,
        string destinationFolder,
        string language,
        bool recursive,
        string[] extensions,
        string ollamaInstance,
        string ollamaModel,
        string ollamaToken)
    {
        logger.LogInformation(
            "Translating files from {sourceFolder} to {lang} and will be saved to {destinationFolder}.", sourceFolder,
            language, destinationFolder);

        var sourceFiles = Directory.GetFiles(sourceFolder, "*.*",
                recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Where(f => !new FileInfo(f).DirectoryName?.EndsWith(".trash") ?? false);

        var sourceIFilesToTranslate = sourceFiles
            .Where(file => extensions.Any(ext =>
                string.Equals(Path.GetExtension(file).TrimStart('.'), ext, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        logger.LogInformation("Found {count} files to translate.", sourceIFilesToTranslate.Length);

        foreach (var sourceFile in sourceIFilesToTranslate)
        {
            var sourceContent = await File.ReadAllTextAsync(sourceFile);

            logger.LogInformation("Translating {sourceFile}...", sourceFile);
            var translatedContent = await ollamaTranslateEngine.TranslateAsync(
                sourceContent,
                language,
                ollamaInstance,
                ollamaModel,
                ollamaToken);
            var destinationFile = sourceFile.Replace(sourceFolder, destinationFolder);

            var destinationDirectory = Path.GetDirectoryName(destinationFile);
            if (!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory!);
            }

            logger.LogInformation("Saving translated content to {destinationFile}...", destinationFile);
            await File.WriteAllTextAsync(destinationFile, translatedContent);
        }
    }
}