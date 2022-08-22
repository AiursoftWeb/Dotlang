using CoreTranslator.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace CoreTranslator
{
    public class StartUp
    {
        public StartUp()
        {

        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(t => t.AddConsole());
            services.AddScoped<BingTranslator>();
            services.AddScoped<DocumentAnalyser>();
            services.AddScoped<TranslatorCore>();
        }

        public void Configure(BingTranslator translator)
        {
            Console.WriteLine("Enter your bing API key:");
            var key = Console.ReadLine()?.Trim() ?? throw new NullReferenceException();
            translator.Init(key);
        }
    }
}
