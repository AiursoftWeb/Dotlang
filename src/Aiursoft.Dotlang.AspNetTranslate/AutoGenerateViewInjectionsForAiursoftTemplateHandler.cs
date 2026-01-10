using System.CommandLine;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.CommandFramework.Services;
using Aiursoft.Dotlang.AspNetTranslate.Services;
using Aiursoft.Dotlang.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aiursoft.Dotlang.AspNetTranslate;

public class AutoGenerateViewInjectionsForAiursoftTemplateHandler : ExecutableCommandHandlerBuilder
{
    protected override string Name => "auto-generate-view-injections-for-aiursoft-template";

    protected override string Description => "The command to scan all Controllers for [RenderInNavBar] attributes and extract strings to inject into ViewModelArgsInjector._useless_for_localizer() method.";

     protected override Task Execute(ParseResult context)
    {
        var verbose = context.GetValue(CommonOptionsProvider.VerboseOption);
        var dryRun = context.GetValue(CommonOptionsProvider.DryRunOption);
        var path = context.GetValue(CommonOptionsProvider.PathOptions)!;
        var hostBuilder = ServiceBuilder.CreateCommandHostBuilder<StartUp>(verbose);
        hostBuilder.ConfigureServices(services =>
        {
            services.Configure<TranslateOptions>(options =>
            {
                options.OllamaInstance = string.Empty;
                options.OllamaModel = string.Empty;
                options.OllamaToken = string.Empty;
            });
        });
        var sp = hostBuilder.Build().Services;
        var entry = sp.GetRequiredService<TranslateEntry>();
        return entry.AutoGenerateViewInjectionsAsync(path, !dryRun);
    }

    protected override Option[] GetCommandOptions()
    {
        return
        [
            CommonOptionsProvider.VerboseOption,
            CommonOptionsProvider.DryRunOption,
            CommonOptionsProvider.PathOptions,
        ];
    }
}
