using System.Text.RegularExpressions;

namespace Aiursoft.Dotlang.AspNetTranslate.Services;

public class ParsedCshtml
{
    private static readonly Regex SingleAtDirective = new(@"^@(?!@)", RegexOptions.Compiled);

    public IReadOnlyList<CshtmlLine> Lines { get; }

    public ParsedCshtml(string cshtmlContent)
    {
        var list = new List<CshtmlLine>();
        var razorDepth = 0;

        foreach (var rawLine in cshtmlContent.Split('\n'))
        {
            var trimmed = rawLine.TrimStart();
            bool isRazor;

            if (razorDepth > 0)
            {
                isRazor = true;
            }
            else if (trimmed.StartsWith("@Localizer") || trimmed.StartsWith("@Html") || trimmed.StartsWith("@foreach") ||
                     trimmed.StartsWith("@if") || trimmed.StartsWith("@for") || trimmed.StartsWith("@while") ||
                     trimmed.StartsWith("@switch") || trimmed.StartsWith("@await RenderSection") || trimmed.StartsWith("@RenderLayout"))
            {
                // This is a hack here. Treat @Localizer and @Html as Html lines to let AngleSharp parse entire HTML part correctly.
                isRazor = false;
            }
            else if (trimmed.StartsWith("@{"))
            {
                isRazor = true;
            }
            // 仅当以 @ 开头且不含 HTML 标签，才算单行 Razor 指令
            else if (SingleAtDirective.IsMatch(trimmed) &&
                     !Regex.IsMatch(trimmed, @"<\s*[a-z][^>]*>", RegexOptions.Compiled))
            {
                isRazor = true;
            }
            else
            {
                isRazor = false;
            }

            list.Add(new CshtmlLine(rawLine, isRazor ? CshtmlLineType.Razor : CshtmlLineType.Html));

            if (isRazor)
            {
                razorDepth += CountBraceDelta(trimmed);
                if (razorDepth < 0) razorDepth = 0;
            }
        }

        Lines = list;
    }


    /// <summary>
    /// 统计一行里 “{” 的数量减去 “}” 的数量
    /// </summary>
    private static int CountBraceDelta(string line)
    {
        var opens = line.Count(c => c == '{');
        var closes = line.Count(c => c == '}');
        return opens - closes;
    }
}