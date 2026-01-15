using Aiursoft.Dotlang.Shared;
using Microsoft.Extensions.Logging;

namespace Aiursoft.Dotlang.AspNetTranslate.Services;

public class FolderFilesTranslateEngine(ILogger<FolderFilesTranslateEngine> logger, OllamaBasedTranslatorEngine ollamaTranslateEngine)
{
    public async Task TranslateAsync(
        string sourceFolder,
        string destinationFolder,
        string language,
        bool recursive,
        string[] extensions,
        bool skipExistingFiles)
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
            var destinationFile = sourceFile.Replace(sourceFolder, destinationFolder);

            if (skipExistingFiles && File.Exists(destinationFile))
            {
                logger.LogInformation("Skipping {sourceFile} because {destinationFile} already exists.", sourceFile, destinationFile);
                continue;
            }

            var sourceContent = await File.ReadAllTextAsync(sourceFile);

            logger.LogInformation("Translating {sourceFile}...", sourceFile);
            var translatedContent = await ollamaTranslateEngine.TranslateAsync(
                sourceContent,
                language);
            
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
