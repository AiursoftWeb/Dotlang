using Aiursoft.Canon;
using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.GptClient;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.Dotlang.OllamaTranslate;

public class Startup : IStartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<FolderFilesTranslateEngine>();
        services.AddGptClient();
        services.AddHttpClient();
        services.AddScoped<OllamaBasedTranslatorEngine>();
        services.AddTaskCanon();
    }
}
