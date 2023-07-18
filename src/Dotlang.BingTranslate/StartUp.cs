using Aiursoft.Dotlang.Core.Abstracts;
using Aiursoft.Dotlang.BingTranslate.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.Dotlang.BingTranslate;

public class StartUp : IStartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<BingTranslator>();
        services.AddTransient<DocumentAnalyser>();
    }
}
