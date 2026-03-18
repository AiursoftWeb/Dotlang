using System.CommandLine;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.CommandFramework.Services;
using Aiursoft.Dotlang.AspNetTranslate.Services;
using Aiursoft.Dotlang.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aiursoft.Dotlang.AspNetTranslate;

public class AutoDedupResxKeysHandler : ExecutableCommandHandlerBuilder
{
    protected override string Name => "auto-dedup-resx-keys";

    protected override string Description => "The command to scan all .resx files in a directory and remove duplicate keys.";

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
        var host = hostBuilder.Build();
        var sp = host.Services;
        var cancellationToken = sp.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
        var entry = sp.GetRequiredService<TranslateEntry>();
        return entry.AutoDedupResxKeysAsync(path, !dryRun, cancellationToken);
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
