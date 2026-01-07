using Aiursoft.Canon;

namespace Aiursoft.Dotlang.Shared;

public class CachedTranslateEngine(
    RetryEngine retryEngine,
    CacheService cache,
    OllamaBasedTranslatorEngine engine)
{
    public virtual async Task<string> TranslateWordInParagraphAsync(
        string sourceContent,
        string word,
        string language)
    {
        return await cache.RunWithCache(
            $"translate-word-{language}-{sourceContent.GetHashCode()}-{word.GetHashCode()}-cache",
            () =>
                retryEngine.RunWithRetry(_ => engine.TranslateWordInParagraphAsync(sourceContent, word, language), attempts: 5)
        );
    }
}
