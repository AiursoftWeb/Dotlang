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
        string language,
        CancellationToken cancellationToken = default)
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
                var translated = await TranslateSingleChunkAsync(chunk.Content, targetLanguage, cancellationToken);
                result.Append(translated);
            }
            else
            {
                result.Append(chunk.Content);
            }
        }
        return result.ToString();
    }

    public async IAsyncEnumerable<string> TranslateStreamAsync(
        string sourceContent,
        string language,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var targetLanguage = LanguageMetadata.SupportedCultures.TryGetValue(language, out var fullName)
            ? $"{language}, {fullName}"
            : language;

        var chunks = shredder.Shred(sourceContent);
        foreach (var chunk in chunks)
        {
            if (chunk.Type == ChunkType.Translatable)
            {
                await foreach (var part in TranslateSingleChunkStreamAsync(chunk.Content, targetLanguage, cancellationToken))
                {
                    yield return part;
                }
            }
            else
            {
                yield return chunk.Content;
            }
        }
    }

    private async IAsyncEnumerable<string> TranslateSingleChunkStreamAsync(
        string sourceContent,
        string targetLanguage,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var leadingWhitespace = new string(sourceContent.TakeWhile(char.IsWhiteSpace).ToArray());
        var trailingWhitespace = new string(sourceContent.Reverse().TakeWhile(char.IsWhiteSpace).Reverse().ToArray());
        var trimmedSource = sourceContent.Trim();
        if (trimmedSource.Length > 2000)
        {
            trimmedSource = trimmedSource.Substring(0, 2000);
        }

        if (string.IsNullOrWhiteSpace(trimmedSource))
        {
            yield return sourceContent;
            yield break;
        }

        yield return leadingWhitespace;

        var message = Prompt.Replace("{CONTENT}", trimmedSource).Replace("{LANG}", targetLanguage);
        var content = new OpenAiRequestModel
        {
            Model = options.Value.OllamaModel,
            Stream = true,
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
            @"Calling Ollama stream to translate: ""{trimmedSource}"", Instance: ""{instance}"", Model: ""{model}""",
            trimmedSource, options.Value.OllamaInstance, options.Value.OllamaModel);

        var buffer = new StringBuilder();
        var inCodeBlock = false;
        var skippingHeader = false;
        var firstPart = true;

        await foreach (var chunk in chatClient.AskModelStream(content, options.Value.OllamaInstance, options.Value.OllamaToken, cancellationToken))
        {
            buffer.Append(chunk);

            if (!inCodeBlock)
            {
                var currentText = buffer.ToString();
                var codeBlockStart = currentText.IndexOf("```", StringComparison.Ordinal);
                if (codeBlockStart >= 0)
                {
                    inCodeBlock = true;
                    skippingHeader = true;
                    // Drop everything before and including the opening backticks
                    buffer.Remove(0, codeBlockStart + 3);
                }
            }

            if (inCodeBlock && skippingHeader)
            {
                var currentText = buffer.ToString();
                var firstNewLine = currentText.IndexOf('\n');
                if (firstNewLine >= 0)
                {
                    // Found the newline, header is over.
                    buffer.Remove(0, firstNewLine + 1);
                    skippingHeader = false;
                }
                else if (currentText.Length > 20 || currentText.Contains(' '))
                {
                    // Too long or contains space, probably not a language identifier.
                    skippingHeader = false;
                }
            }

            if (inCodeBlock && !skippingHeader)
            {
                var currentText = buffer.ToString();
                var codeBlockEnd = currentText.IndexOf("```", StringComparison.Ordinal);
                if (codeBlockEnd >= 0)
                {
                    var resultPart = currentText.Substring(0, codeBlockEnd);
                    yield return resultPart.Trim('\n', '\r');
                    buffer.Clear();
                    inCodeBlock = false; // Reset for next potential chunk (though usually one per prompt)
                    break; 
                }
                else
                {
                    // Still in code block, yield what we have so far if it's safe (not potentially part of closing backticks)
                    var safeLength = buffer.Length - 3; 
                    if (safeLength > 0)
                    {
                        var toYield = buffer.ToString().Substring(0, safeLength);
                        if (firstPart)
                        {
                            toYield = toYield.TrimStart('\n', '\r');
                            firstPart = false;
                        }
                        yield return toYield;
                        buffer.Remove(0, safeLength);
                    }
                }
            }
        }

        yield return trailingWhitespace;
    }

    private async Task<string> TranslateSingleChunkAsync(
        string sourceContent,
        string targetLanguage,
        CancellationToken cancellationToken)
    {
        var leadingWhitespace = new string(sourceContent.TakeWhile(char.IsWhiteSpace).ToArray());
        var trailingWhitespace = new string(sourceContent.Reverse().TakeWhile(char.IsWhiteSpace).Reverse().ToArray());
        var trimmedSource = sourceContent.Trim();
        if (trimmedSource.Length > 2000)
        {
            trimmedSource = trimmedSource.Substring(0, 2000);
        }

        if (string.IsNullOrWhiteSpace(trimmedSource))
        {
            return sourceContent;
        }

        var message = Prompt.Replace("{CONTENT}", trimmedSource).Replace("{LANG}", targetLanguage);
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
            @"Calling Ollama to translate: ""{trimmedSource}"", Instance: ""{instance}"", Model: ""{model}""",
            trimmedSource, options.Value.OllamaInstance, options.Value.OllamaModel);
        var aiResponseRaw = await retryEngine.RunWithRetry(async _ =>
        {
            var result = await chatClient.AskModel(content, options.Value.OllamaInstance, options.Value.OllamaToken,
                cancellationToken);
            return ExtractTranslation(result.GetAnswerPart());
        }, attempts: 5);
        logger.LogInformation(@"Ollama translation result: ""{result}""", aiResponseRaw);
        return leadingWhitespace + aiResponseRaw + trailingWhitespace;
    }

    public async Task<string> TranslateWordInParagraphAsync(
        string sourceContent,
        string word,
        string language,
        CancellationToken cancellationToken = default)
    {
        var targetLanguage = LanguageMetadata.SupportedCultures.TryGetValue(language, out var fullName) 
            ? $"{language}, {fullName}" 
            : language;

        var leadingWhitespace = new string(word.TakeWhile(char.IsWhiteSpace).ToArray());
        var trailingWhitespace = new string(word.Reverse().TakeWhile(char.IsWhiteSpace).Reverse().ToArray());
        var trimmedWord = word.Trim();

        if (string.IsNullOrWhiteSpace(trimmedWord))
        {
            return word;
        }

        // Limit context to 30 lines before and after the line containing the word.
        var lines = sourceContent.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        var targetLineIndex = Array.FindIndex(lines, l => l.Contains(trimmedWord));
        if (targetLineIndex != -1)
        {
            var startLine = Math.Max(0, targetLineIndex - 30);
            var endLine = Math.Min(lines.Length - 1, targetLineIndex + 30);
            sourceContent = string.Join("\n", lines.Skip(startLine).Take(endLine - startLine + 1));
        }

        var message = PromptWord
            .Replace("{CONTENT}", sourceContent)
            .Replace("{LANG}", targetLanguage)
            .Replace("{WORD}", trimmedWord);
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

        logger.LogInformation(@"Calling Ollama to translate: ""{trimmedWord}"" to ""{lang}"", Instance: ""{instance}"", Model: ""{model}""",
            trimmedWord, language, options.Value.OllamaInstance, options.Value.OllamaModel);
        var aiResponseRaw = await retryEngine.RunWithRetry(async _ =>
        {
            var result = await chatClient.AskModel(content, options.Value.OllamaInstance, options.Value.OllamaToken, cancellationToken);
            return ExtractTranslation(result.GetAnswerPart());
        }, attempts: 5);
        logger.LogInformation(@"Ollama translation result of ""{trimmedWord}"" is ""{result}""", trimmedWord, aiResponseRaw);
        return leadingWhitespace + aiResponseRaw + trailingWhitespace;
    }

    public string ExtractTranslation(string rawResult)
    {
        var resultText = rawResult.Trim();
        var start = resultText.IndexOf("```", StringComparison.Ordinal);
        var end = resultText.LastIndexOf("```", StringComparison.Ordinal);

        string inner;
        if (start >= 0)
        {
            if (end > start)
            {
                inner = resultText.Substring(start + 3, end - start - 3);
            }
            else
            {
                // Found only one set of backticks, assume it's the opening one.
                inner = resultText.Substring(start + 3);
            }
        }
        else
        {
            // No backticks found, assume the whole response is the translation.
            inner = resultText;
        }

        if (!inner.StartsWith("\n") && !inner.StartsWith("\r"))
        {
            var firstNewLine = inner.IndexOf('\n');
            if (firstNewLine >= 0)
            {
                var firstLine = inner.Substring(0, firstNewLine).Trim();
                if (firstLine.All(c => char.IsLetterOrDigit(c) || c == '-'))
                {
                    inner = inner.Substring(firstNewLine + 1);
                }
            }
        }

        var resultTextWithoutCodeBlock = inner.Trim('\n', '\r', ' ');

        if (string.IsNullOrWhiteSpace(resultTextWithoutCodeBlock))
        {
            logger.LogWarning("LLM returned empty translation result. Raw string: {RawResult}", rawResult);
            throw new InvalidOperationException(
                $"The translation result is empty. Please check the input content and language. Actual result: \n{rawResult}");
        }

        return resultTextWithoutCodeBlock;
    }
}
