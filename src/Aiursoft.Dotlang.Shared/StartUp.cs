using Aiursoft.Canon;
using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.GptClient;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.Dotlang.Shared;

public class StartUp : IStartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddHttpClient();
        services.AddTaskCanon();
        services.AddScoped<OllamaBasedTranslatorEngine>();
        services.AddScoped<CachedTranslateEngine>();
        services.AddGptClient();
    }
}
