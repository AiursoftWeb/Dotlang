namespace Aiursoft.Dotlang.BingTranslate.Models;

public enum StringType
{
    Tag,
    Razor,
    Text
}

public class HtmlPart(string content)
{
    public StringType StringType { get; set; }
    public string Content { get; set; } = content;
    public override string ToString() => Content;
}
