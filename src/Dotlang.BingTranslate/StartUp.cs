using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.Dotlang.BingTranslate.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.Dotlang.BingTranslate;

public class StartUp : IStartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddHttpClient();
        services.AddTransient<TranslateEntry>();
        services.AddTransient<BingTranslator>();
        services.AddTransient<DocumentAnalyser>();
    }
}
