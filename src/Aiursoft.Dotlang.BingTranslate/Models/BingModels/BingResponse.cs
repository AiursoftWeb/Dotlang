namespace Aiursoft.Dotlang.BingTranslate.Models.BingModels;

public class BingResponse
{
    public DetectedLanguage DetectedLanguage { get; set; } = new DetectedLanguage();

    public IReadOnlyCollection<TranslationsItem>? Translations { get; set; }
}
