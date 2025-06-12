using Aiursoft.Canon;

namespace Aiursoft.Dotlang.Shared;

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