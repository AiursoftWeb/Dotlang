using Aiursoft.Canon;
using Aiursoft.GptClient.Abstractions;
using Aiursoft.GptClient.Services;
using Microsoft.Extensions.Logging;

namespace Aiursoft.Dotlang.OllamaTranslate;

public class OllamaBasedTranslatorEngine(
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

    public async Task<string> TranslateAsync(
        string sourceContent,
        string language,
        string ollamaInstance,
        string ollamaModel,
        string ollamaToken)
    {
        var message = Prompt.Replace("{CONTENT}", sourceContent).Replace("{LANG}", language);
        var content = new OpenAiModel
        {
            Model = ollamaModel,
            Messages =
            [
                new MessagesItem
                {
                    Role = "user",
                    Content = message
                }
            ]
        };

        logger.LogInformation("Calling Ollama to translate... Raw content: {content}, Instance: {instance}, Model: {model}",
            content, ollamaInstance, ollamaModel);
        var aiResponseRaw = await retryEngine.RunWithRetry(async _ =>
        {
            var result = await chatClient.AskModel(content, ollamaInstance, ollamaToken, CancellationToken.None);
            var resultText = result.GetAnswerPart();
            var resultTextWithoutCodeBlock = resultText.Trim('`', ' ', '\n');
            if (string.IsNullOrWhiteSpace(resultTextWithoutCodeBlock))
            {
                throw new InvalidOperationException(
                    "The translation result is empty. Please check the input content and language.");
            }

            return resultTextWithoutCodeBlock;
        });
        logger.LogInformation("Ollama translation result: {result}", aiResponseRaw);
        return aiResponseRaw;
    }
}
