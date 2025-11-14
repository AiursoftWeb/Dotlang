using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.CommandFramework.Services;
using Aiursoft.Dotlang.AspNetTranslate.Services;
using Aiursoft.Dotlang.Shared;

namespace Aiursoft.Dotlang.AspNetTranslate;

public class GenerateResxForCsharpHandler : ExecutableCommandHandlerBuilder
{
    private readonly Option<string> TargetLangs = new(
        name: "--languages",
        aliases: ["-l"])
    {
        DefaultValueFactory = _ => string.Join(",", GenerateResxForViewsHandler.SupportedCultures.Keys),
        Description = "The target languages code. Connect with ','. For example: zh-CN,en-US,ja-JP",
        Required = true
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

    private static readonly Option<int> ConcurrentRequestsOption = new(
        name: "--concurrent-requests",
        aliases: ["-c"])
    {
        DefaultValueFactory = _ => 1,
        Description = "The max concurrent requests to Ollama.",
        Required = true
    };

    protected override string Name => "generate-resx-csharp";

    protected override string Description => "The command to start translation for C# files in a .NET project.";

     protected override Task Execute(ParseResult context)
    {
        var verbose = context.GetValue(CommonOptionsProvider.VerboseOption);
        var dryRun = context.GetValue(CommonOptionsProvider.DryRunOption);
        var path = context.GetValue(CommonOptionsProvider.PathOptions)!;
        var targetLangs = context.GetValue(TargetLangs)!;
        var concurrentRequests = context.GetValue(ConcurrentRequestsOption);
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
