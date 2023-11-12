using Aiursoft.Dotlang.Core.Framework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.CommandFramework.Services;
using Aiursoft.Dotlang.BingTranslate.Services;

namespace Aiursoft.Dotlang.BingTranslate;

public class TranslateHandler : CommandHandler
{
    private readonly Option<string> _bingApiKey = new(
        aliases: new[] { "--key", "-k" },
        description: "The Bing API Key.")
    {
        IsRequired = true
    };

    private readonly Option<string> _targetLang = new(
        aliases: new[] { "--language", "-l" },
        description: "The target language code. For example: zh, en, ja")
    {
        IsRequired = true
    };

    public override string Name => "translate";

    public override string Description => "The command to start translation based on Bing Translate.";

    public override Option[] GetCommandOptions()
    {
        return new Option[]
        {
            _bingApiKey,
            _targetLang
        };
    }

    public override void OnCommandBuilt(Command command)
    {
        command.SetHandler(
            ExecuteOverride,
            OptionsProvider.PathOptions,
            OptionsProvider.DryRunOption,
            OptionsProvider.VerboseOption,
            _bingApiKey,
            _targetLang);
    }

    private Task ExecuteOverride(string path, bool dryRun, bool verbose, string key, string targetLang)
    {
        var hostBuilder = ServiceBuilder.CreateCommandHostBuilder<StartUp>(verbose);
        hostBuilder.ConfigureServices(services =>
        {
            services.AddSingleton(new TranslateOptions { APIKey = key, TargetLanguage = targetLang });
        });
        var sp = hostBuilder.Build().Services;
        var entry = sp.GetRequiredService<TranslateEntry>();
        return entry.OnServiceStartedAsync(path, !dryRun);
    }
}
