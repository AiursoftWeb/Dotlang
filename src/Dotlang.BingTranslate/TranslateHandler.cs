using Aiursoft.Dotlang.Core.Framework;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

namespace Aiursoft.Dotlang.BingTranslate;

public class TranslateHandler : ServiceCommandHandler<TranslateEntry, StartUp>
{
    private readonly Option<string> _bingAPIKey = new(
        aliases: new[] { "--key", "-k" },
        description: "The Bing API Key.")
    {
        IsRequired = true
    };

    private readonly Option<string> _targetLang = new(
        aliases: new[] { "--language", "-l" },
        description: "The target langage code. For example: zh, en, ja")
    {
        IsRequired = true
    };

    public override string Name => "translate";

    public override string Description => "The command to start translation based on Bing Translate.";

    public override Option[] GetOptions()
    {
        return new Option[]
        {
            _bingAPIKey,
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
            _bingAPIKey,
            _targetLang);
    }

    private Task ExecuteOverride(string path, bool dryRun, bool verbose, string key, string targetLang)
    {
        var services = BuildServices(verbose);
        services.AddSingleton(new TranslateOptions { APIKey = key, TargetLanguage = targetLang });
        return RunFromServices(services, path, dryRun);
    }
}
