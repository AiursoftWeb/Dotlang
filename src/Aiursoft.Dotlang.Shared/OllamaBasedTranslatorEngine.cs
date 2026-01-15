using System.Text;
using Aiursoft.Canon;
using Aiursoft.GptClient.Abstractions;
using Aiursoft.GptClient.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aiursoft.Dotlang.Shared;

public class OllamaBasedTranslatorEngine(
    IOptions<TranslateOptions> options,
    RetryEngine retryEngine,
    ILogger<OllamaBasedTranslatorEngine> logger,
    ChatClient chatClient,
    MarkdownShredder shredder)
{
    private const string Prompt =
        """

        I am a programmer working on a software localization project. I need to localize some files into the target language {LANG}. Unfortunately, I don't understand the {LANG} language or the local culture. Therefore, I need you to act as an experienced translator who can accurately, fluently, and carefully choose words to translate some files I see into the {LANG} language. Please output the translated conclusion and nothing else. Do not output any other content!

        The content you need to translate is:
        ```
        {CONTENT}
        ```

        Human names, Nicknames, Proper nouns, symbols, trademarks, etc. must be output as it is and do not translate them! If you don't understand what you want to translate, please output it as it is!

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

        Please translate only the sentence or word `{WORD}` in the paragraph into the target language {LANG}.

        Human names, Nicknames, Proper nouns, symbols, trademarks, etc. must be output as it is and do not translate them! So please consider if the sentence or word `{WORD}` is one of them.

        If you don't understand what you want to translate, please output it as it is!

        Do **NOT** output any other content. Only output the translated content and wrap it in three backticks (```), like this:

        ```
        Translated word here...
        ```

        """;

    public async Task<string> TranslateAsync(
        string sourceContent,
        string language)
    {
        var targetLanguage = LanguageMetadata.SupportedCultures.TryGetValue(language, out var fullName)
            ? $"{language}, {fullName}"
            : language;

        var chunks = shredder.Shred(sourceContent);
        var result = new StringBuilder();
        foreach (var chunk in chunks)
        {
            if (chunk.Type == ChunkType.Translatable)
            {
                var translated = await TranslateSingleChunkAsync(chunk.Content, targetLanguage);
                result.Append(translated);
            }
            else
            {
                result.Append(chunk.Content);
            }
        }
        return result.ToString();
    }

    private async Task<string> TranslateSingleChunkAsync(
        string sourceContent,
        string targetLanguage)
    {
        var message = Prompt.Replace("{CONTENT}", sourceContent).Replace("{LANG}", targetLanguage);
        var content = new OpenAiRequestModel
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
            sourceContent, options.Value.OllamaInstance, options.Value.OllamaModel);
        var aiResponseRaw = await retryEngine.RunWithRetry(async _ =>
        {
            var result = await chatClient.AskModel(content, options.Value.OllamaInstance, options.Value.OllamaToken,
                CancellationToken.None);
            var resultText = result.GetAnswerPart();
            if (!resultText.Trim().StartsWith("```") || !resultText.Trim().EndsWith("```"))
            {
                throw new InvalidOperationException(
                    "The translation result is not wrapped in code block. Please check the input content and language.");
            }

            var resultTextWithoutCodeBlock = resultText.Trim('`', ' ', '\n');
            if (string.IsNullOrWhiteSpace(resultTextWithoutCodeBlock))
            {
                throw new InvalidOperationException(
                    "The translation result is empty. Please check the input content and language.");
            }

            return resultTextWithoutCodeBlock;
        }, attempts: 5);
        logger.LogInformation(@"Ollama translation result: ""{result}""", aiResponseRaw);
        return aiResponseRaw;
    }

    public async Task<string> TranslateWordInParagraphAsync(
        string sourceContent,
        string word,
        string language)
    {
        var targetLanguage = LanguageMetadata.SupportedCultures.TryGetValue(language, out var fullName) 
            ? $"{language}, {fullName}" 
            : language;
        var message = PromptWord
            .Replace("{CONTENT}", sourceContent)
            .Replace("{LANG}", targetLanguage)
            .Replace("{WORD}", word);
        var content = new OpenAiRequestModel
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

        logger.LogInformation(@"Calling Ollama to translate: ""{word}"" to ""{lang}"", Instance: ""{instance}"", Model: ""{model}""",
            word, language, options.Value.OllamaInstance, options.Value.OllamaModel);
        var aiResponseRaw = await retryEngine.RunWithRetry(async _ =>
        {
            var result = await chatClient.AskModel(content, options.Value.OllamaInstance, options.Value.OllamaToken, CancellationToken.None);
            var resultText = result.GetAnswerPart();

            if (!resultText.Trim().StartsWith("```") || !resultText.Trim().EndsWith("```"))
            {
                throw new InvalidOperationException(
                    "The translation result is not wrapped in code block. Please check the input content and language.");
            }
            var resultTextWithoutCodeBlock = resultText.Trim('`', ' ', '\n');
            if (string.IsNullOrWhiteSpace(resultTextWithoutCodeBlock))
            {
                throw new InvalidOperationException(
                    "The translation result is empty. Please check the input content and language.");
            }

            return resultTextWithoutCodeBlock;
        }, attempts: 5);
        logger.LogInformation(@"Ollama translation result of ""{word}"" is ""{result}""", word, aiResponseRaw);
        return aiResponseRaw;
    }
}
