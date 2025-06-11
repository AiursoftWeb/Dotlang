using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html;
using AngleSharp.Html.Parser;

namespace Aiursoft.Dotlang.AspNetTranslate.Services;

public enum CshtmlLineType
{
    Razor,
    Html
}

public class CshtmlLine
{
    public string Raw { get; }
    public CshtmlLineType Type { get; }

    public CshtmlLine(string raw, CshtmlLineType type)
    {
        Raw = raw;
        Type = type;
    }

    public override string ToString() => $"{Type}: {Raw}";
}

public class ParsedCshtml
{
    private static readonly Regex SingleAtDirective = new(@"^@(?!@)", RegexOptions.Compiled);

    public IReadOnlyList<CshtmlLine> Lines { get; }

    public ParsedCshtml(string cshtmlContent)
    {
        var list = new List<CshtmlLine>();
        int razorDepth = 0;

        foreach (var rawLine in cshtmlContent.Split('\n'))
        {
            var trimmed = rawLine.TrimStart();
            bool isRazor;

            if (razorDepth > 0)
            {
                // 只要深度 > 0，就一定是 Razor 段
                isRazor = true;
            }
            else if (trimmed.StartsWith("@{"))
            {
                // 显式的 @{ … } 代码块起始
                isRazor = true;
            }
            else if (SingleAtDirective.IsMatch(trimmed))
            {
                // 单行 Razor 指令：@using, @inject, @section … 等
                isRazor = true;
            }
            else
            {
                isRazor = false;
            }

            // 添加到列表
            list.Add(new CshtmlLine(rawLine, isRazor ? CshtmlLineType.Razor : CshtmlLineType.Html));

            // 更新深度（只有 Razor 段计数）
            if (isRazor)
            {
                razorDepth += CountBraceDelta(trimmed);
                // 保证不会降到负数
                if (razorDepth < 0)
                {
                    razorDepth = 0;
                }
            }
        }

        Lines = list;
    }

    /// <summary>
    /// 统计一行里 “{” 的数量减去 “}” 的数量
    /// </summary>
    private static int CountBraceDelta(string line)
    {
        var opens  = line.Count(c => c == '{');
        var closes = line.Count(c => c == '}');
        return opens - closes;
    }
}


public class CshtmlLocalizer
{
    private readonly HtmlParser _parser = new();
    private static readonly Regex SingleAtRegex = new("(?<!@)@(?!@)", RegexOptions.Compiled);

    public (string TransformedCshtml, List<string> Keys) Process(string cshtmlContent)
    {
        var parsed = new ParsedCshtml(cshtmlContent);
        var keys = new List<string>();
        var output = new StringBuilder();

        // 1. 按行分段
        var segments = new List<List<CshtmlLine>>();
        if (parsed.Lines.Count > 0)
        {
            var seg = new List<CshtmlLine> { parsed.Lines[0] };
            for (int i = 1; i < parsed.Lines.Count; i++)
            {
                var line = parsed.Lines[i];
                if (line.Type == seg[0].Type)
                {
                    seg.Add(line);
                }
                else
                {
                    segments.Add(seg);
                    seg = new List<CshtmlLine> { line };
                }
            }
            segments.Add(seg);
        }

        var formatter = new HtmlMarkupFormatter();

        // 2. 分段处理
        foreach (var seg in segments)
        {
            if (seg[0].Type == CshtmlLineType.Razor)
            {
                // Razor 段，原样输出
                foreach (var line in seg)
                {
                    output.AppendLine(line.Raw);
                }
            }
            else
            {
                // HTML 段：拼成一个完整片段
                var htmlBlock = string.Join("\n", seg.Select(l => l.Raw));

                // 用一个临时 <div> 容器 ParseFragment
                var tempDoc = _parser.ParseDocument("<div></div>");
                var container = tempDoc.QuerySelector("div")!;
                var fragment = _parser.ParseFragment(htmlBlock, container).ToArray();
                foreach (var node in fragment)
                {
                    container.AppendChild(node);
                }

                // 规范化空白字符
                NormalizeWhitespace(container);

                // 本地化文本节点
                WrapTextNodes(container, keys);

                // 序列化容器子节点（去掉外层 <div>）
                using var sw = new StringWriter();
                foreach (var child in container.ChildNodes)
                {
                    child.ToHtml(sw, formatter);
                }
                output.Append(sw.ToString());
            }
        }

        return (output.ToString(), keys.Distinct().ToList());
    }

    /// <summary>
    /// 递归折叠所有文本节点中的连续空白字符为单个空格，并在内容前后去掉空格。
    /// </summary>
    private void NormalizeWhitespace(INode node)
    {
        foreach (var child in node.ChildNodes.ToArray())
        {
            if (child.NodeType == NodeType.Text)
            {
                var txt = child.TextContent;
                // 折叠空白，去前后空格
                var norm = Regex.Replace(txt, @"\s+", " ").Trim();
                // 如果折叠后已经为空，则移除该节点；否则替换内容
                if (string.IsNullOrEmpty(norm))
                    child.RemoveFromParent();
                else
                    child.TextContent = norm;
            }
            else
            {
                NormalizeWhitespace(child);
            }
        }
    }

    // ——和你原来一模一样的 WrapTextNodes ——
    private void WrapTextNodes(INode node, List<string> keys)
    {
        foreach (var child in node.ChildNodes.ToArray())
        {
            if (child.NodeType == NodeType.Text)
            {
                var txt = child.TextContent;
                if (SingleAtRegex.IsMatch(txt)) continue;
                var trimmed = txt.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                keys.Add(trimmed);
                var newNode = node.Owner?.CreateTextNode($"@Localizer[\"{trimmed}\"]");
                child.ReplaceWith(newNode);
            }
            else
            {
                WrapTextNodes(child, keys);
            }
        }
    }
}
