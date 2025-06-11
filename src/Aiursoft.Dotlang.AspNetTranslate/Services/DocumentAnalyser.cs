using Aiursoft.Dotlang.AspNetTranslate.Models;

namespace Aiursoft.Dotlang.AspNetTranslate.Services;

public class DocumentAnalyser
{
    public static List<HtmlPart> AnalyseFile(string html)
    {
        var document = new List<HtmlPart>();
        while (html.Trim().Length > 0)
        {
            var (newPart, remainingHtml) = GetNextPart(html);
            html = remainingHtml;
            newPart.Content = newPart.Content.Replace('\\', '/');
            document.Add(newPart);
        }
        return document;
    }

    private static (HtmlPart, string) GetNextPart(string html)
    {
        var part = new HtmlPart(string.Empty);
        if (html.Trim().Length < 1)
        {
            throw new Exception();
        }
        switch (html.Trim()[0])
        {
            case '<':
                part.StringType = StringType.Tag;
                part.Content = html[..(html.IndexOf('>') + 1)];
                return (part, html[(html.IndexOf('>') + 1)..]);
            case '@':
            case '}':
            {
                part.StringType = StringType.Razor;
                var endPoint = html.IndexOf('<');
                if (endPoint > 0)
                {
                    part.Content = html[..endPoint];
                    return (part, html[endPoint..]);
                }
                else
                {
                    part.Content = html;
                    return (part, "");
                }
            }
            default:
            {
                part.StringType = StringType.Text;
                var endPoint = html.IndexOf('<');
                if (endPoint > 0)
                {
                    part.Content = html[..endPoint];
                    return (part, html[endPoint..]);
                }
                else
                {
                    part.Content = html;
                    return (part, "");
                }
            }
        }
    }
}
