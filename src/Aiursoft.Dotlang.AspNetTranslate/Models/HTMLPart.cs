namespace Aiursoft.Dotlang.AspNetTranslate.Models;

public class HtmlPart(string content)
{
    public StringType StringType { get; set; }
    public string Content { get; set; } = content;
    public override string ToString() => Content;
}
