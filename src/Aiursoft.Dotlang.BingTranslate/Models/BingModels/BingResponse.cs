namespace Aiursoft.Dotlang.BingTranslate.Models.BingModels;

public class BingResponse
{
    public DetectedLanguage DetectedLanguage { get; set; } = new();

    public IReadOnlyCollection<TranslationsItem>? Translations { get; set; }
}
