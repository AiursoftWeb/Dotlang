using Aiursoft.Canon;
using Aiursoft.Dotlang.BingTranslate;
using Aiursoft.GptClient.Abstractions;
using Aiursoft.GptClient.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aiursoft.Dotlang.OllamaTranslate;

public class CachedTranslateEngine(
    CacheService cache,
    OllamaBasedTranslatorEngine engine)
{
    public async Task<string> TranslateAsync(
        string sourceContent,
        string language)
    {
        return await cache.RunWithCache($"translate-{language}-{sourceContent.GetHashCode()}-cache",
            async () => await engine.TranslateAsync(sourceContent, language));
    }

    public async Task<string> TranslateWordInParagraphAsync(
        string sourceContent,
        string word,
        string language)
    {
        return await cache.RunWithCache(
            $"translate-word-{language}-{sourceContent.GetHashCode()}-{word.GetHashCode()}-cache",
            async () => await engine.TranslateWordInParagraphAsync(sourceContent, word, language));
    }
}
public class OllamaBasedTranslatorEngine(
    IOptions<TranslateOptions> options,
    RetryEngine retryEngine,
    ILogger<OllamaBasedTranslatorEngine> logger,
    ChatClient chatClient)
{
    private const string Prompt =
        """

        I am a programmer working on a software localization project. I need to localize some files into the target language {LANG}. Unfortunately, I don't understand the {LANG} language or the local culture. Therefore, I need you to act as an experienced translator who can accurately, fluently, and carefully choose words to translate some files I see into the {LANG} language. Please output the translated conclusion and nothing else. Do not output any other content!

        The content you need to translate is:
        ```
        {CONTENT}
        ```

        Please translate the content into the target language {LANG}. Do **NOT** output any other sentences or content. Only output the translated content and wrap it in three backticks (```), like this:

        ```
        Translated content here...
        ```

        """;


    private const string PromptWord =
        """

        I am a programmer working on a software localization project. I need to localize some words into the target language {LANG}. Unfortunately, I don't understand the {LANG} language or the local culture. Therefore, I need you to act as an experienced translator who can accurately, fluently, and carefully choose words to translate some files I see into the {LANG} language. Please output the translated conclusion and nothing else. Do not output any other content!

        I'm translating a sentence in a paragraph. The sentence is `{WORD}`. The paragraph is:

        ```
        {CONTENT}
        ```

        Please translate only the sentence `{WORD}` into the target language {LANG}. Do **NOT** output any other content. Only output the translated content and wrap it in three backticks (```), like this:

        ```
        Translated word here...
        ```

        """;

    public async Task<string> TranslateAsync(
        string sourceContent,
        string language)
    {
        var message = Prompt.Replace("{CONTENT}", sourceContent).Replace("{LANG}", language);
        var content = new OpenAiModel
        {
            Model = options.Value.OllamaModel,
            Messages =
            [
                new MessagesItem
                {
                    Role = "user",
                    Content = message
                }
            ]
        };

        logger.LogInformation(
            @"Calling Ollama to translate: ""{sourceContent}"", Instance: ""{instance}"", Model: ""{model}""",
            content, options.Value.OllamaInstance, options.Value.OllamaModel);
        var aiResponseRaw = await retryEngine.RunWithRetry(async _ =>
        {
            var result = await chatClient.AskModel(content, options.Value.OllamaInstance, options.Value.OllamaToken, CancellationToken.None);
            var resultText = result.GetAnswerPart();
            var resultTextWithoutCodeBlock = resultText.Trim('`', ' ', '\n');
            if (string.IsNullOrWhiteSpace(resultTextWithoutCodeBlock))
            {
                throw new InvalidOperationException(
                    "The translation result is empty. Please check the input content and language.");
            }

            return resultTextWithoutCodeBlock;
        });
        logger.LogInformation(@"Ollama translation result: ""{result}""", aiResponseRaw);
        return aiResponseRaw;
    }

    public async Task<string> TranslateWordInParagraphAsync(
        string sourceContent,
        string word,
        string language)
    {
        var message = PromptWord
            .Replace("{CONTENT}", sourceContent)
            .Replace("{LANG}", language)
            .Replace("{WORD}", word);
        var content = new OpenAiModel
        {
            Model = options.Value.OllamaModel,
            Messages =
            [
                new MessagesItem
                {
                    Role = "user",
                    Content = message
                }
            ]
        };

        logger.LogInformation(@"Calling Ollama to translate: ""{word}"", Instance: ""{instance}"", Model: ""{model}""",
            word, options.Value.OllamaInstance, options.Value.OllamaModel);
        var aiResponseRaw = await retryEngine.RunWithRetry(async _ =>
        {
            var result = await chatClient.AskModel(content, options.Value.OllamaInstance, options.Value.OllamaToken, CancellationToken.None);
            var resultText = result.GetAnswerPart();
            var resultTextWithoutCodeBlock = resultText.Trim('`', ' ', '\n');
            if (string.IsNullOrWhiteSpace(resultTextWithoutCodeBlock))
            {
                throw new InvalidOperationException(
                    "The translation result is empty. Please check the input content and language.");
            }

            return resultTextWithoutCodeBlock;
        });
        logger.LogInformation(@"Ollama translation result of ""{word}"" is ""{result}""", word, aiResponseRaw);
        return aiResponseRaw;
    }
}
