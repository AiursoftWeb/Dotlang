using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;
using System.CommandLine.Invocation;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.CommandFramework.Services;
using Aiursoft.Dotlang.AspNetTranslate.Services;
using Aiursoft.Dotlang.Shared;

namespace Aiursoft.Dotlang.AspNetTranslate;

public class GenerateResxForCsharpHandler : ExecutableCommandHandlerBuilder
{
    private readonly Option<string> TargetLangs = new(
        aliases: ["--languages", "-l"],
        getDefaultValue: () => string.Join(",", GenerateResxForViewsHandler.SupportedCultures.Keys),
        description: "The target languages code. Connect with ','. For example: zh-CN,en-US,ja-JP")
    {
        IsRequired = true
    };

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

    private static readonly Option<int> ConcurrentRequestsOption = new(
        ["--concurrent-requests", "-c"],
        getDefaultValue: () => 1,
        "The max concurrent requests to Ollama.")
    {
        IsRequired = true
    };

    protected override string Name => "generate-resx-csharp";

    protected override string Description => "The command to start translation for C# files in a .NET project.";

    protected override Task Execute(InvocationContext context)
    {
        var verbose = context.ParseResult.GetValueForOption(CommonOptionsProvider.VerboseOption);
        var dryRun = context.ParseResult.GetValueForOption(CommonOptionsProvider.DryRunOption);
        var path = context.ParseResult.GetValueForOption(CommonOptionsProvider.PathOptions)!;
        var targetLangs = context.ParseResult.GetValueForOption(TargetLangs)!;
        var concurrentRequests = context.ParseResult.GetValueForOption(ConcurrentRequestsOption);
        var hostBuilder = ServiceBuilder.CreateCommandHostBuilder<StartUp>(verbose);
        hostBuilder.ConfigureServices(services =>
        {
            services.AddTransient<TranslateEntry>();
            services.AddScoped<CshtmlLocalizer>();
            services.AddScoped<CSharpKeyExtractor>(); // For C# files
            services.AddTransient<DocumentAnalyser>();
            services.Configure<TranslateOptions>(options =>
            {
                options.OllamaInstance = context.ParseResult.GetValueForOption(OllamaInstanceOption)!;
                options.OllamaModel = context.ParseResult.GetValueForOption(OllamaModelOption)!;
                options.OllamaToken = context.ParseResult.GetValueForOption(OllamaTokenOption)!;
            });
        });
        var sp = hostBuilder.Build().Services;
        var entry = sp.GetRequiredService<TranslateEntry>();
        var targetLangsArray = targetLangs
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(lang => lang.Trim())
            .ToArray();

        // Calling the new method for C# files. This method does not exist yet.
        return entry.StartLocalizeContentInCSharpAsync(path, targetLangsArray, !dryRun, concurrentRequests);
    }

    protected override Option[] GetCommandOptions()
    {
        return
        [
            CommonOptionsProvider.VerboseOption,
            CommonOptionsProvider.DryRunOption,
            CommonOptionsProvider.PathOptions,
            TargetLangs,
            OllamaInstanceOption,
            OllamaModelOption,
            OllamaTokenOption,
            ConcurrentRequestsOption
        ];
    }
}
