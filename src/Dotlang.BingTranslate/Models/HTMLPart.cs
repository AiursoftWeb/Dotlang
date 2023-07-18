namespace Aiursoft.Dotlang.BingTranslate.Models;

public enum StringType
{
    Tag,
    Razor,
    Text
}

public class HTMLPart
{
    public HTMLPart(string content)
    {
        this.Content = content;
    }

    public StringType StringType { get; set; }
    public string Content { get; set; }
    public override string ToString() => this.Content;
}
