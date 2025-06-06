using System.CommandLine;
using System.CommandLine.Invocation;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.CommandFramework.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.Dotlang.OllamaTranslate;

public class AiTranslateHandler : ExecutableCommandHandlerBuilder
{
    protected override string Name => "ai-translate";
    protected override string Description => "Translate all files in a directory using GPT.";

    private static readonly Option<string> SourcePathOptions = new(
        ["--source", "-s"],
        "Path of the folder to translate files.")
    {
        IsRequired = true
    };

    private static readonly Option<string> DestinationPathOptions = new(
        ["--destination", "-d"],
        "Path of the folder to save translated files.")
    {
        IsRequired = true
    };

    private static readonly Option<string> LanguageOptions = new(
        ["--language", "-l"],
        "The target language code. For example: zh_CN, en_US, ja_JP")
    {
        IsRequired = true
    };

    private static readonly Option<bool> RecursiveOption = new(
        ["--recursive", "-r"],
        () => false,
        "Recursively search for files in subdirectories.");

    private static readonly Option<string[]> ExtensionsOption = new(
        ["--extensions", "-e"],
        () => ["html"],
        "Extensions of files to translate.");

    private static readonly Option<string> OllamaInstanceOption = new(
        ["--instance"],
        "The Ollama instance to use.")
    {
        IsRequired = true
    };

    private static readonly Option<string> OllamaModelOption = new(
        ["--model"],
        "The Ollama model to use.")
    {
        IsRequired = true
    };

    private static readonly Option<string> OllamaTokenOption = new(
        ["--token"],
        "The Ollama token to use.")
    {
        IsRequired = true
    };

    protected override IEnumerable<Option> GetCommandOptions()
    {
        return
        [
            CommonOptionsProvider.VerboseOption,
            SourcePathOptions,
            DestinationPathOptions,
            LanguageOptions,
            RecursiveOption,
            ExtensionsOption,
            OllamaInstanceOption,
            OllamaModelOption,
            OllamaTokenOption
        ];
    }

    protected override async Task Execute(InvocationContext context)
    {
        var verbose = context.ParseResult.GetValueForOption(CommonOptionsProvider.VerboseOption);
        var sourcePath = context.ParseResult.GetValueForOption(SourcePathOptions)!;
        var destinationPath = context.ParseResult.GetValueForOption(DestinationPathOptions)!;
        var language = context.ParseResult.GetValueForOption(LanguageOptions)!;
        var recursive = context.ParseResult.GetValueForOption(RecursiveOption);
        var extensions = context.ParseResult.GetValueForOption(ExtensionsOption);
        var ollamaInstance = context.ParseResult.GetValueForOption(OllamaInstanceOption)!;
        var ollamaModel = context.ParseResult.GetValueForOption(OllamaModelOption)!;
        var ollamaToken = context.ParseResult.GetValueForOption(OllamaTokenOption)!;

        if (!(extensions?.Any() ?? false))
            throw new ArgumentException("At least one extension should be provided for --extensions.");

        var services = ServiceBuilder
            .CreateCommandHostBuilder<Startup>(verbose)
            .Build()
            .Services;

        var translateEngine = services.GetRequiredService<TranslateEngine>();

        var absoluteSourcePath = Path.IsPathRooted(sourcePath)
            ? Path.GetFullPath(sourcePath)
            : Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, sourcePath));
        var absoluteDestinationPath = Path.IsPathRooted(destinationPath)
            ? Path.GetFullPath(destinationPath)
            : Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, destinationPath));

        await translateEngine.TranslateAsync(
            sourceFolder: absoluteSourcePath,
            destinationFolder: absoluteDestinationPath,
            language: language,
            recursive: recursive,
            extensions: extensions,
            ollamaInstance: ollamaInstance,
            ollamaModel: ollamaModel,
            ollamaToken: ollamaToken);
    }
}

//
//     public class OpenAiService
//     {
//         private readonly HttpClient _httpClient;
//         private readonly ILogger _logger;
//         private readonly string _token;
//         private readonly string _instance;
//         private readonly string _model;
//

//
//         public OpenAiService(
//             HttpClient httpClient,
//             ILogger<OpenAiService> logger,
//             string token,
//             string instance,
//             string model)
//         {
//             _httpClient = httpClient;
//             _httpClient.Timeout = TimeSpan.FromMinutes(5);
//             _logger = logger;
//             _token =  token;
//             _instance =  instance;
//             _model =  model;
//         }
//
//         public async Task<string> TranslateContent(string content, string lang)
//         {
//
//             var response = await Ask(prompt);
//             var responseText =  response.Choices.First().Message!.Content!;
//             if (responseText.StartsWith("```"))
//             {
//                 responseText = responseText.Substring(3);
//             }
//             if (responseText.EndsWith("```"))
//             {
//                 responseText = responseText.Substring(0, responseText.Length - 3);
//             }
//
//             return responseText;
//         }
//
//         private async Task<CompletionData> Ask(string content)
//         {
//             if (string.IsNullOrWhiteSpace(_token))
//             {
//                 throw new ArgumentNullException(nameof(_token));
//             }
//
//             _logger.LogInformation("Asking OpenAi...");
//             var model = new OpenAiModel
//             {
//                 Messages = new List<MessagesItem>
//                 {
//                     new()
//                     {
//                         Content = content,
//                         Role = "user"
//                     }
//                 },
//                 Model = _model
//             };
//
//             var json = JsonSerializer.Serialize(model);
//             var request = new HttpRequestMessage(HttpMethod.Post, $"{_instance}/v1/chat/completions")
//             {
//                 Content = new StringContent(json, Encoding.UTF8, "application/json")
//             };
//
//             request.Headers.Add("Authorization", $"Bearer {_token}");
//             var response = await _httpClient.SendAsync(request);
//             try
//             {
//                 response.EnsureSuccessStatusCode();
//                 var responseJson = await response.Content.ReadAsStringAsync();
//                 var responseModel = JsonSerializer.Deserialize<CompletionData>(responseJson);
//                 return responseModel!;
//             }
//             catch (HttpRequestException raw)
//             {
//                 var remoteError = await response.Content.ReadAsStringAsync();
//                 throw new HttpRequestException(remoteError, raw);
//             }
//         }
//     }
//
