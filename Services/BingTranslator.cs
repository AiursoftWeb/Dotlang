using CoreTranslator.Services.BingModels;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;
using System.Collections.Generic;

namespace CoreTranslator.Services
{
    public class BingTranslator
    {
        private readonly ILogger<BingTranslator> _logger;
        private static string _apiKey;

        public BingTranslator(
            ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<BingTranslator>();
        }

        public void Init(string apiKey)
        {
            _apiKey = apiKey;
        }

        private string CallTranslateAPI(string inputJson, string targetLanguage)
        {
            var apiAddress = $"https://api.cognitive.microsofttranslator.com/translate?api-version=3.0&to={targetLanguage}";
            var client = new RestClient(apiAddress);
            var request = new RestRequest(Method.POST);
            request
                .AddHeader("Ocp-Apim-Subscription-Key", _apiKey)
                .AddHeader("Content-Type", "application/json")
                .AddParameter("undefined", inputJson, ParameterType.RequestBody);

            var json = client.Execute(request).Content;
            return json;
        }

        public string CallTranslate(string input, string targetLanguage)
        {
            var inputSource = new List<Translation>
            {
                new Translation { Text = input }
            };
            var bingResponse = CallTranslateAPI(JsonConvert.SerializeObject(inputSource), targetLanguage);
            var result = JsonConvert.DeserializeObject<List<BingResponse>>(bingResponse);
            _logger.LogInformation($"\t\tCalled Bing: {input} - {result[0].Translations[0].Text}");
            return result[0].Translations[0].Text;
        }
    }
}
