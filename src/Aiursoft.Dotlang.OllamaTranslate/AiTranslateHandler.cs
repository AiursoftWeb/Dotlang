using System.CommandLine;
using System.CommandLine.Invocation;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.CommandFramework.Services;
using Aiursoft.Dotlang.BingTranslate;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.Dotlang.OllamaTranslate;

public class AiTranslateHandler : ExecutableCommandHandlerBuilder
{
    protected override string Name => "ai-translate";
    protected override string Description => "Translate all files in a directory using GPT.";

    private static readonly Option<string> SourcePathOptions = new(
        ["--source", "-s"],
        "Path of the folder to translate files.")
    {
        IsRequired = true
    };

    private static readonly Option<string> DestinationPathOptions = new(
        ["--destination", "-d"],
        "Path of the folder to save translated files.")
    {
        IsRequired = true
    };

    private static readonly Option<string> LanguageOptions = new(
        ["--language", "-l"],
        "The target language code. For example: zh_CN, en_US, ja_JP")
    {
        IsRequired = true
    };

    private static readonly Option<bool> RecursiveOption = new(
        ["--recursive", "-r"],
        () => false,
        "Recursively search for files in subdirectories.");

    private static readonly Option<string[]> ExtensionsOption = new(
        ["--extensions", "-e"],
        () => ["html"],
        "Extensions of files to translate.");

    private static readonly Option<string> OllamaInstanceOption = new(
        ["--instance"],
        "The Ollama instance to use.")
    {
        IsRequired = true
    };

    private static readonly Option<string> OllamaModelOption = new(
        ["--model"],
        "The Ollama model to use.")
    {
        IsRequired = true
    };

    private static readonly Option<string> OllamaTokenOption = new(
        ["--token"],
        "The Ollama token to use.")
    {
        IsRequired = true
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
            OllamaTokenOption
        ];
    }

    protected override async Task Execute(InvocationContext context)
    {
        var verbose = context.ParseResult.GetValueForOption(CommonOptionsProvider.VerboseOption);
        var sourcePath = context.ParseResult.GetValueForOption(SourcePathOptions)!;
        var destinationPath = context.ParseResult.GetValueForOption(DestinationPathOptions)!;
        var language = context.ParseResult.GetValueForOption(LanguageOptions)!;
        var recursive = context.ParseResult.GetValueForOption(RecursiveOption);
        var extensions = context.ParseResult.GetValueForOption(ExtensionsOption);
        var ollamaInstance = context.ParseResult.GetValueForOption(OllamaInstanceOption)!;
        var ollamaModel = context.ParseResult.GetValueForOption(OllamaModelOption)!;
        var ollamaToken = context.ParseResult.GetValueForOption(OllamaTokenOption)!;

        if (!(extensions?.Any() ?? false))
            throw new ArgumentException("At least one extension should be provided for --extensions.");

        var services = ServiceBuilder
            .CreateCommandHostBuilder<Startup>(verbose)
            .Build()
            .Services;

        var translateEngine = services.GetRequiredService<FolderFilesTranslateEngine>();

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
            extensions: extensions);
    }
}
