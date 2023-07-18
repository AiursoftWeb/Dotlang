using System.CommandLine;
using Aiursoft.Dotlang.Core.Abstracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aiursoft.Dotlang.Core.Framework;

public abstract class ServiceCommandHandler<TE, TS> : CommandHandler
    where TE : class, IEntryService
    where TS : class, IStartUp, new()
{
    public override void OnCommandBuilt(Command command)
    {
        command.SetHandler(
            Execute,
            OptionsProvider.PathOptions,
            OptionsProvider.DryRunOption,
            OptionsProvider.VerboseOption);
    }

    public Task Execute(string path, bool dryRun, bool verbose)
    {
        var services = BuildServices(verbose);
        return RunFromServices(services, path, dryRun);
    }

    protected virtual ServiceCollection BuildServices(bool verbose)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            logging
                .AddFilter("Microsoft.Extensions", LogLevel.Warning)
                .AddFilter("System", LogLevel.Warning);
            logging.AddSimpleConsole(options => 
            {
                options.IncludeScopes = verbose;
                options.SingleLine = true;
                options.TimestampFormat = "mm:ss ";
            });
            logging.SetMinimumLevel(verbose ? LogLevel.Trace : LogLevel.Warning);
        });

        var startUp = new TS();
        services.AddMemoryCache();
        services.AddHttpClient();
        startUp.ConfigureServices(services);
        services.AddTransient<TE>();
        return services;
    }

    protected virtual Task RunFromServices(ServiceCollection services, string path, bool dryRun)
    {
        var serviceProvider = services.BuildServiceProvider();
        var service = serviceProvider.GetRequiredService<TE>();
        var logger = serviceProvider.GetRequiredService<ILogger<TE>>();

        var fullPath = Path.GetFullPath(path);
        logger.LogTrace($"Starting service: '{typeof(TE).Name}'. Full path is: '{fullPath}', Dry run is: '{dryRun}'.");
        return service.OnServiceStartedAsync(fullPath, shouldTakeAction: !dryRun);
    }
}
