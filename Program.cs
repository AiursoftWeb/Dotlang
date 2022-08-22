using CoreTranslator.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CoreTranslator
{
    public class Program
    {
        static void Main(string[] args)
        {
            BuildApplication()
                .GetRequiredService<TranslatorCore>()
                .DoWork();
        }

        static ServiceProvider BuildApplication()
        {
            var startUp = new StartUp();
            var services = new ServiceCollection();

            startUp.ConfigureServices(services);

            var serviceProvider = services.BuildServiceProvider();
            var bingTranslator = serviceProvider.GetRequiredService<BingTranslator>();
            startUp.Configure(bingTranslator);
            return serviceProvider;
        }
    }
}
