using Aiursoft.Canon;
using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.Dotlang.AspNetTranslate.Services;
using Aiursoft.Dotlang.Shared;
using Aiursoft.GptClient;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.Dotlang.AspNetTranslate;

public class StartUp : IStartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<DataAnnotationKeyExtractor>();
        services.AddScoped<FolderFilesTranslateEngine>();
        services.AddTransient<TranslateEntry>();
        services.AddScoped<CshtmlLocalizer>();
        services.AddScoped<CSharpKeyExtractor>();
        services.AddTransient<DocumentAnalyser>();
        services.AddMemoryCache();
        services.AddHttpClient();
        services.AddTaskCanon();
        services.AddScoped<OllamaBasedTranslatorEngine>();
        services.AddScoped<CachedTranslateEngine>();
        services.AddGptClient();
    }
}
