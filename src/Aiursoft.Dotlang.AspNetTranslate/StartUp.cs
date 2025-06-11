using Aiursoft.Canon;
using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.Dotlang.AspNetTranslate.Services;
using Aiursoft.Dotlang.OllamaTranslate;
using Aiursoft.GptClient;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.Dotlang.AspNetTranslate;

public class StartUp : IStartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddHttpClient();
        services.AddTaskCanon();
        services.AddTransient<TranslateEntry>();
        services.AddTransient<DocumentAnalyser>();
        services.AddScoped<FolderFilesTranslateEngine>();
        services.AddScoped<OllamaBasedTranslatorEngine>();
        services.AddScoped<CachedTranslateEngine>();
        services.AddGptClient();
    }
}
