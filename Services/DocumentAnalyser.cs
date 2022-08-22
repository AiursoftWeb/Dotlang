using CoreTranslator.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace CoreTranslator.Services
{
    public class DocumentAnalyser
    {
        public DocumentAnalyser()
        {

        }

        public List<HTMLPart> AnalyseFile(string html)
        {
            var document = new List<HTMLPart>();
            while (html.Trim().Length > 0)
            {
                var (newpart, remainingHtml) = GetNextPart(html);
                html = remainingHtml;
                newpart.Content = newpart.Content.Replace('\\', '/');
                document.Add(newpart);
            }
            return document;
        }

        public (HTMLPart, string) GetNextPart(string html)
        {
            var part = new HTMLPart(string.Empty);
            if (html.Trim().Length < 1)
            {
                throw new Exception();
            }
            if (html.Trim()[0] == '<')
            {
                part.StringType = StringType.Tag;
                part.Content = html.Substring(0, html.IndexOf('>') + 1);
                return (part, html.Substring(html.IndexOf('>') + 1));
            }
            else if (html.Trim()[0] == '@' || html.Trim()[0] == '}')
            {
                part.StringType = StringType.Razor;
                var endPoint = html.IndexOf('<');
                if (endPoint > 0)
                {
                    part.Content = html.Substring(0, endPoint);
                    return (part, html.Substring(endPoint));
                }
                else
                {
                    part.Content = html;
                    return (part, "");
                }
            }
            else
            {
                part.StringType = StringType.Text;
                var endPoint = html.IndexOf('<');
                if (endPoint > 0)
                {
                    part.Content = html.Substring(0, endPoint);
                    return (part, html.Substring(endPoint));
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
