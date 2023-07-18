using Aiursoft.Dotlang.BingTranslate.Models.BingModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RestSharp;

namespace Aiursoft.Dotlang.BingTranslate.Services;

public class BingTranslator
{
    private readonly ILogger<BingTranslator> _logger;
    private readonly TranslateOptions _options;
    private readonly Dictionary<string, string> _cache = new Dictionary<string, string>
    {
        {"x","x"},
        {"Aiursoft","Aiursoft"},
        {"Operational","一切正常"}
    };

    private readonly Dictionary<string, string> _toReplace = new Dictionary<string, string>
    {
        {"艾尔索特","Aiursoft"},
        {"艾乌索特","Aiursoft"},
        {"阿凡达","头像"}
    };

    public BingTranslator(
        IOptions<TranslateOptions> options,
        ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<BingTranslator>();
        _options = options.Value;
    }

    private string CallTranslateAPI(string inputJson, string targetLanguage)
    {
        var apiAddress = $"https://api.cognitive.microsofttranslator.com/translate?api-version=3.0&to={targetLanguage}";
        var client = new RestClient(apiAddress);
        var request = new RestRequest(apiAddress, Method.Post);
        request
            .AddHeader("Ocp-Apim-Subscription-Key", _options.APIKey ?? throw new NullReferenceException())
            .AddHeader("Content-Type", "application/json")
            .AddParameter("undefined", inputJson, ParameterType.RequestBody);

        var json = client.Execute(request).Content;
        return json ?? throw new NullReferenceException();
    }

    public string? CallTranslate(string input, string targetLanguage)
    {
        if (_cache.TryGetValue(input, out string? cacheResult))
        {
            return cacheResult;
        }
        var inputSource = new List<Translation>
        {
            new Translation { Text = input }
        };
        var bingResponse = CallTranslateAPI(JsonConvert.SerializeObject(inputSource), targetLanguage);
        var result = JsonConvert.DeserializeObject<List<BingResponse>>(bingResponse) ?? throw new NullReferenceException();
        _logger.LogInformation($"\t\tCalled Bing: {input} - {result[0].Translations?.First().Text}");
        var toReturn = result[0].Translations?.First().Text;
        foreach (var replaceRecord in _toReplace)
        {
            toReturn = toReturn?.Replace(replaceRecord.Key, replaceRecord.Value);
        }
        return toReturn;
    }
}
