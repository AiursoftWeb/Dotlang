using Aiursoft.Canon;
using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.Dotlang.BingTranslate.Services;
using Aiursoft.Dotlang.OllamaTranslate;
using Aiursoft.GptClient;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.Dotlang.BingTranslate;

public class StartUp : IStartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddHttpClient();
        services.AddTaskCanon();
        services.AddTransient<TranslateEntry>();
        services.AddTransient<BingTranslator>();
        services.AddTransient<DocumentAnalyser>();
        services.AddScoped<FolderFilesTranslateEngine>();
        services.AddScoped<OllamaBasedTranslatorEngine>();
        services.AddGptClient();
    }
}
