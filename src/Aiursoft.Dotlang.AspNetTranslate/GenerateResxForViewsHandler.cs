using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.CommandFramework.Services;
using Aiursoft.Dotlang.AspNetTranslate.Services;
using Aiursoft.Dotlang.Shared;

namespace Aiursoft.Dotlang.AspNetTranslate;

public class GenerateResxForViewsHandler : ExecutableCommandHandlerBuilder
{
    public static readonly Dictionary<string, string> SupportedCultures = new()
    {
        { "en-GB", "English (United Kingdom)" },
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
        { "tr-TR", "Türkçe (Türkiye)" },
        { "ro-RO", "Română (România)" },
        { "da-DK", "Dansk (Danmark)" },
        { "uk-UA", "Українська (Україна)" },
        { "id-ID", "Bahasa Indonesia (Indonesia)" },
        { "fi-FI", "Suomi (Suomi)" },
        { "hi-IN", "हिन्दी (भारत)" },
        { "el-GR", "Ελληνικά (Ελλάδα)" }
    };

    private readonly Option<string> TargetLangs = new(
        name: "--languages",
        aliases: ["-l"])
    {
        DefaultValueFactory = _ => string.Join(",", SupportedCultures.Keys),
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

    protected override string Name => "generate-resx-view";

    protected override string Description => "The command to start translation on an ASP.NET Core project.";

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
        return entry.StartLocalizeContentInCsHtmlAsync(path, targetLangsArray, !dryRun, concurrentRequests);
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
