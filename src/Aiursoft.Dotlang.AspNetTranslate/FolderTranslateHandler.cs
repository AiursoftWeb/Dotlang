using System.CommandLine;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.CommandFramework.Services;
using Aiursoft.Dotlang.AspNetTranslate.Services;
using Aiursoft.Dotlang.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aiursoft.Dotlang.AspNetTranslate;

public class FolderTranslateHandler : ExecutableCommandHandlerBuilder
{
    protected override string Name => "folder-translate";
    protected override string Description => "Translate all files in a directory using GPT.";

    private static readonly Option<string> SourcePathOptions = new(
        name: "--source",
        aliases: ["-s"])
    {
        Description = "Path of the folder to translate files.",
        Required = true
    };

    private static readonly Option<string> DestinationPathOptions = new(
        name: "--destination",
        aliases: ["-d"])
    {
        Description = "Path of the folder to save translated files.",
        Required = true
    };

    private static readonly Option<string> LanguageOptions = new(
        name: "--language",
        aliases: ["-l"])
    {
        Description = "The target language code. For example: zh_CN, en_US, ja_JP",
        Required = true
    };

    private static readonly Option<bool> RecursiveOption = new(
        name: "--recursive",
        aliases: ["-r"])
    {
        DefaultValueFactory = _ => false,
        Description = "Recursively search for files in subdirectories."
    };

    private static readonly Option<string[]> ExtensionsOption = new(
        name: "--extensions",
        aliases: ["-e"])
    {
        DefaultValueFactory = _ => ["html"],
        Description = "Extensions of files to translate."
    };

    private static readonly Option<string> OllamaInstanceOption = new(
        name: "--instance")
    {
        Description = "The Ollama instance to use.",
        Required = true
    };

    private static readonly Option<string> OllamaModelOption = new(
        name: "--model")
    {
        Description = "The Ollama model to use.",
        Required = true
    };

    private static readonly Option<string> OllamaTokenOption = new(
        name: "--token")
    {
        Description = "The Ollama token to use.",
        Required = true
    };

    private static readonly Option<bool> SkipExistingFilesOption = new(
        name: "--skip-existing-files",
        aliases: ["-k"])
    {
        DefaultValueFactory = _ => false,
        Description = "Skip existing files."
    };

    protected override IEnumerable<Option> GetCommandOptions()
    {
        return
        [
            CommonOptionsProvider.VerboseOption,
            SourcePathOptions,
            DestinationPathOptions,
            LanguageOptions,
            RecursiveOption,
            ExtensionsOption,
            OllamaInstanceOption,
            OllamaModelOption,
            OllamaTokenOption,
            SkipExistingFilesOption
        ];
    }

    protected override async Task Execute(ParseResult context)
    {
        var verbose = context.GetValue(CommonOptionsProvider.VerboseOption);
        var sourcePath = context.GetValue(SourcePathOptions)!;
        var destinationPath = context.GetValue(DestinationPathOptions)!;
        var language = context.GetValue(LanguageOptions)!;
        var recursive = context.GetValue(RecursiveOption);
        var extensions = context.GetValue(ExtensionsOption);
        var skipExistingFiles = context.GetValue(SkipExistingFilesOption);

        if (!(extensions?.Any() ?? false))
            throw new ArgumentException("At least one extension should be provided for --extensions.");

        var hostBuilder = ServiceBuilder.CreateCommandHostBuilder<StartUp>(verbose);
        hostBuilder.ConfigureServices(services =>
        {
            services.Configure<TranslateOptions>(options =>
            {
                options.OllamaInstance = context.GetValue(OllamaInstanceOption)!;
                options.OllamaModel = context.GetValue(OllamaModelOption)!;
                options.OllamaToken = context.GetValue(OllamaTokenOption)!;
            });
        });
        var sp = hostBuilder.Build().Services;

        var translateEngine = sp.GetRequiredService<FolderFilesTranslateEngine>();

        var absoluteSourcePath = Path.IsPathRooted(sourcePath)
            ? Path.GetFullPath(sourcePath)
            : Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, sourcePath));
        var absoluteDestinationPath = Path.IsPathRooted(destinationPath)
            ? Path.GetFullPath(destinationPath)
            : Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, destinationPath));

        await translateEngine.TranslateAsync(
            sourceFolder: absoluteSourcePath,
            destinationFolder: absoluteDestinationPath,
            language: language,
            recursive: recursive,
            extensions: extensions,
            skipExistingFiles: skipExistingFiles);
    }
}
