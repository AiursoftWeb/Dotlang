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

public class AspNetCoreProjectTranslateHandler : ExecutableCommandHandlerBuilder
{
    public static readonly Dictionary<string, string> SupportedCultures = new()
    {
        { "zh-CN", "中文 (中国大陆)" },
        { "zh-TW", "中文 (台灣)" },
        { "zh-HK", "中文 (香港)" },
        { "ja-JP", "日本語 (日本)" },
        { "ko-KR", "한국어 (대한민국)" },
        { "vi-VN", "Tiếng Việt (Việt Nam)" },
        { "th-TH", "ภาษาไทย (ประเทศไทย)" },
        { "de-DE", "Deutsch (Deutschland)" },
        { "fr-FR", "Français (France)" },
        { "es-ES", "Español (España)" },
        { "ru-RU", "Русский (Россия)" },
        { "it-IT", "Italiano (Italia)" },
        { "pt-PT", "Português (Portugal)" },
        { "pt-BR", "Português (Brasil)" },
        { "ar-SA", "العربية (المملكة العربية السعودية)" },
        { "nl-NL", "Nederlands (Nederland)" },
        { "sv-SE", "Svenska (Sverige)" },
        { "pl-PL", "Polski (Polska)" },
        { "tr-TR", "Türkçe (Türkiye)" }
    };

    private readonly Option<string> _targetLangs = new(
        aliases: ["--languages", "-l"],
        getDefaultValue: () => string.Join(",", SupportedCultures.Keys),
        description: "The target languages code. Connect with ','. For example: zh_CN,en_US,ja_JP")
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

    protected override string Name => "translate-aspnet";

    protected override string Description => "The command to start translation on an ASP.NET Core project.";

    protected override Task Execute(InvocationContext context)
    {
        var verbose = context.ParseResult.GetValueForOption(CommonOptionsProvider.VerboseOption);
        var dryRun = context.ParseResult.GetValueForOption(CommonOptionsProvider.DryRunOption);
        var path = context.ParseResult.GetValueForOption(CommonOptionsProvider.PathOptions)!;
        var targetLangs = context.ParseResult.GetValueForOption(_targetLangs)!;
        var hostBuilder = ServiceBuilder.CreateCommandHostBuilder<StartUp>(verbose);
        hostBuilder.ConfigureServices(services =>
        {
            services.AddTransient<TranslateEntry>();
            services.AddScoped<CshtmlLocalizer>();
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
        return entry.StartTranslateAsync(path, targetLangsArray, !dryRun);
    }

    protected override Option[] GetCommandOptions()
    {
        return
        [
            CommonOptionsProvider.VerboseOption,
            CommonOptionsProvider.DryRunOption,
            CommonOptionsProvider.PathOptions,
            _targetLangs,
            OllamaInstanceOption,
            OllamaModelOption,
            OllamaTokenOption,
        ];
    }
}
