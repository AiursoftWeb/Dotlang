using CoreTranslator.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CoreTranslator
{
    public class Program
    {
        static void Main(string[] args)
        {
            BuildApplication()
                .GetService<TranslatorCore>()
                .DoWork();
        }

        static ServiceProvider BuildApplication()
        {
            var startUp = new StartUp();
            var services = new ServiceCollection();

            startUp.ConfigureServices(services);

            var serviceProvider = services.BuildServiceProvider();
            var bingTranslator = serviceProvider.GetService<BingTranslator>();
            startUp.Configure(bingTranslator);
            return serviceProvider;
        }
    }
}
