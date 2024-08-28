using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.CommandFramework.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aiursoft.Dotlang.GptTranslate;

public class GptTranslateHandler : ExecutableCommandHandlerBuilder
{
    protected override string Name => "gpt-translate";
    protected override string Description => "Translate all files in a directory using GPT.";

    public static readonly Option<string> SourcePathOptions = new(
        ["--source", "-s"],
        "Path of the folder to translate files.")
    {
        IsRequired = true
    };
    
    public static readonly Option<string> DestinationPathOptions = new(
        ["--destination", "-d"],
        "Path of the folder to save translated files.")
    {
        IsRequired = true
    };
    
    public static readonly Option<string> LanguageOptions = new(
        ["--language", "-l"],
        "The target language code. For example: zh_CN, en_US, ja_JP")
    {
        IsRequired = true
    };
    
    public static readonly Option<bool> RecursiveOption = new(
        ["--recursive", "-r"],
        () => false,
        "Recursively search for files in subdirectories.");
    
    public static readonly Option<string[]> ExtensionsOption = new(
        ["--extensions", "-e"],
        () => ["html"],
        "Extensions of files to translate.");
    
    public static readonly Option<string> OpenAiInstanceOption = new(
        ["--instance"],
        "The OpenAI instance to use.")
    {
        IsRequired = true
    };
    
    public static readonly Option<string> OpenAiModelOption = new(
        ["--model"],
        "The OpenAI model to use.")
    {
        IsRequired = true
    };
    
    public static readonly Option<string> OpenAiTokenOption = new(
        ["--token"],
        "The OpenAI token to use.")
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
            OpenAiInstanceOption,
            OpenAiModelOption,
            OpenAiTokenOption
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
        var openAiInstance = context.ParseResult.GetValueForOption(OpenAiInstanceOption)!;
        var openAiModel = context.ParseResult.GetValueForOption(OpenAiModelOption)!;
        var openAiToken = context.ParseResult.GetValueForOption(OpenAiTokenOption)!;
        
        if (!(extensions?.Any() ?? false)) throw new ArgumentException("At least one extension should be provided for --extensions.");
        
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
            openAiInstance: openAiInstance,
            openAiModel: openAiModel,
            openAiToken: openAiToken);
    }
}

public class Startup : IStartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<TranslateEngine>();
    }
}

public class TranslateEngine(ILogger<TranslateEngine> logger, ILogger<OpenAiService> openAiLogger)
{
    public async Task TranslateAsync(
        string sourceFolder, 
        string destinationFolder, 
        string language, 
        bool recursive, 
        string[] extensions,
        string openAiInstance,
        string openAiModel,
        string openAiToken)
    {
        var translator = new OpenAiService(new HttpClient(), openAiLogger, openAiToken, openAiInstance, openAiModel); 
        logger.LogInformation("Translating files from {sourceFolder} to {lang} and will be saved to {destinationFolder}.", sourceFolder, language, destinationFolder);
        
        var sourceFiles = Directory.GetFiles(sourceFolder, "*.*",
                recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Where(f => !new FileInfo(f).DirectoryName?.EndsWith(".trash") ?? false);
        
        var sourceIFilesToTranslate = sourceFiles
            .Where(file => extensions.Any(ext =>
                string.Equals(Path.GetExtension(file).TrimStart('.'), ext, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        logger.LogInformation("Found {count} files to translate.", sourceIFilesToTranslate.Length);
        
        foreach (var sourceFile in sourceIFilesToTranslate)
        {
            var sourceContent = await File.ReadAllTextAsync(sourceFile);
            
            logger.LogInformation("Translating {sourceFile}...", sourceFile);
            var translatedContent = await translator.TranslateContent(sourceContent, language);
            var destinationFile = sourceFile.Replace(sourceFolder, destinationFolder);
            
            var destinationDirectory = Path.GetDirectoryName(destinationFile);
            if (!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory!);
            }
            
            logger.LogInformation("Saving translated content to {destinationFile}...", destinationFile);
            await File.WriteAllTextAsync(destinationFile, translatedContent);
        }
    }
}

    public class OpenAiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly string _token;
        private readonly string _instance;
        private readonly string _model;

        private const string Prompt =
            @"
I am a programmer working on a software localization project. I need to localize some files into the target language {LANG}. Unfortunately, I don't understand the {LANG} language or the local culture. Therefore, I need you to act as an experienced translator who can accurately, fluently, and carefully choose words to translate some files I see into the {LANG} language. Please output the translated conclusion and nothing else. Do not output any other content!

The content you need to translate is:
```
{CONTENT}
```

Please translate the content into the target language {LANG}. Do **NOT** output any other sentences or content. Only output the translated content!";

        public OpenAiService(
            HttpClient httpClient,
            ILogger<OpenAiService> logger,
            string token,
            string instance,
            string model)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
            _logger = logger;
            _token =  token;
            _instance =  instance;
            _model =  model;
        }

        public async Task<string> TranslateContent(string content, string lang)
        {
            var prompt = Prompt.Replace("{CONTENT}", content).Replace("{LANG}", lang);
            var response = await Ask(prompt);
            var responseText =  response.Choices.First().Message!.Content!;
            if (responseText.StartsWith("```"))
            {
                responseText = responseText.Substring(3);
            }
            if (responseText.EndsWith("```"))
            {
                responseText = responseText.Substring(0, responseText.Length - 3);
            }

            return responseText;
        }

        private async Task<CompletionData> Ask(string content)
        {
            if (string.IsNullOrWhiteSpace(_token))
            {
                throw new ArgumentNullException(nameof(_token));
            }

            _logger.LogInformation("Asking OpenAi...");
            var model = new OpenAiModel
            {
                Messages = new List<MessagesItem>
                {
                    new()
                    {
                        Content = content,
                        Role = "user"
                    }
                },
                Model = _model
            };

            var json = JsonSerializer.Serialize(model);
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_instance}/v1/chat/completions")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            request.Headers.Add("Authorization", $"Bearer {_token}");
            var response = await _httpClient.SendAsync(request);
            try
            {
                response.EnsureSuccessStatusCode();
                var responseJson = await response.Content.ReadAsStringAsync();
                var responseModel = JsonSerializer.Deserialize<CompletionData>(responseJson);
                return responseModel!;
            }
            catch (HttpRequestException raw)
            {
                var remoteError = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(remoteError, raw);
            }
        }
    }

    public class MessagesItem
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    public class OpenAiModel
    {
        [JsonPropertyName("messages")]
        public List<MessagesItem> Messages { get; set; } = [];

        [JsonPropertyName("stream")]
        public bool Stream { get; set; } = false;

        [JsonPropertyName("model")] 
        public string? Model { get; set; } = "gpt-3.5-turbo-16k";

        [JsonPropertyName("temperature")] 
        public double Temperature { get; set; } = 0.5;

        [JsonPropertyName("presence_penalty")] 
        public int PresencePenalty { get; set; } = 0;
    }

    public class UsageData
    {
        /// <summary>
        /// The number of prompt tokens used in the request.
        /// </summary>
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        /// <summary>
        /// The number of completion tokens generated in the response.
        /// </summary>
        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        /// <summary>
        /// The total number of tokens used in the request and generated in the response.
        /// </summary>
        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }

        /// <summary>
        /// The number of tokens in the prompt before any adjustments were made.
        /// </summary>
        [JsonPropertyName("pre_token_count")]
        public int PreTokenCount { get; set; }

        /// <summary>
        /// The total number of tokens in the prompt before any adjustments were made.
        /// </summary>
        [JsonPropertyName("pre_total")]
        public int PreTotal { get; set; }

        /// <summary>
        /// The total number of tokens used in the response after adjustments were made.
        /// </summary>
        [JsonPropertyName("adjust_total")]
        public int AdjustTotal { get; set; }

        /// <summary>
        /// The final total number of tokens in the response.
        /// </summary>
        [JsonPropertyName("final_total")]
        public int FinalTotal { get; set; }
    }

    public class MessageData
    {
        /// <summary>
        /// The role of the message, such as "user" or "bot".
        /// </summary>
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        /// <summary>
        /// The content of the message.
        /// </summary>
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    public class ChoicesItemData
    {
        /// <summary>
        /// The message data for this choice.
        /// </summary>
        [JsonPropertyName("message")]
        public MessageData? Message { get; set; }

        /// <summary>
        /// The reason why this choice was selected as the final choice.
        /// </summary>
        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }

        /// <summary>
        /// The index of this choice in the list of choices.
        /// </summary>
        [JsonPropertyName("index")]
        public int Index { get; set; }
    }

    public class CompletionData
    {
        /// <summary>
        /// The ID of the completion.
        /// </summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        /// <summary>
        /// The type of the object, which is always "text_completion".
        /// </summary>
        [JsonPropertyName("object")]
        public string? Object { get; set; }

        /// <summary>
        /// The timestamp when the completion was created.
        /// </summary>
        [JsonPropertyName("created")]
        public int Created { get; set; }

        /// <summary>
        /// The name of the model used to generate the completion.
        /// </summary>
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        /// <summary>
        /// The usage data for this completion.
        /// </summary>
        [JsonPropertyName("usage")]
        public UsageData? Usage { get; set; }

        /// <summary>
        /// The list of choices generated by the completion.
        /// </summary>
        [JsonPropertyName("choices")]
        // ReSharper disable once CollectionNeverUpdated.Global
        public List<ChoicesItemData> Choices { get; set; } = [];
    }

    /// <summary>
    /// Represents the response data from the OpenAI API for a text completion request.
    /// </summary>
    public class TextCompletionResponseData
    {
        /// <summary>
        /// The completion data for this response.
        /// </summary>
        [JsonPropertyName("completion")]
        public CompletionData? Completion { get; set; }
    }
