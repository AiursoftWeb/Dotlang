using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html;
using AngleSharp.Html.Parser;

namespace Aiursoft.Dotlang.AspNetTranslate.Services;

public class CshtmlLocalizer
{
    private readonly HtmlParser _parser = new();
    private static readonly Regex SingleAtRegex = new("(?<!@)@(?!@)", RegexOptions.Compiled);

    private static readonly Regex LocalizerRegex = new Regex(
        @"@Localizer\[\s*""((?:\\.|[^""\\])*)""\s*\]",
        RegexOptions.Compiled
    );


    /// <summary>
    /// Extracts all the keys used in @Localizer["…"] from the given text.
    /// </summary>
    public string[] ExtractLocalizerKeys(string input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));

        return LocalizerRegex
            .Matches(input)
            .Select(m => m.Groups[1].Value)
            .Select(t => t.Replace(@"\""", "\"")) // 处理转义的引号
            .Distinct()
            .ToArray();
    }

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
            for (var i = 1; i < parsed.Lines.Count; i++)
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

        var formatter = new PrettyMarkupFormatter();

        foreach (var seg in segments)
        {
            if (seg[0].Type == CshtmlLineType.Razor)
            {
                // Razor 段，原样输出
                foreach (var line in seg)
                    output.AppendLine(line.Raw);
                continue;
            }

            // HTML 段，先检测是不是“顶层结构”标签段
            var firstTrim = seg[0].Raw.TrimStart();
            if (Regex.IsMatch(firstTrim, @"^(<!DOCTYPE|</?html|</?head|</?body)\b", RegexOptions.IgnoreCase))
            {
                // 直接原样输出这整个段，并加个空行分隔
                foreach (var line in seg)
                    output.AppendLine(line.Raw);
                output.AppendLine();
                continue;
            }

            // === 普通 HTML 片段走解析流程 ===
            var htmlBlock = string.Join("\n", seg.Select(l => l.Raw));
            var tempDoc = _parser.ParseDocument("<div></div>");
            var container = tempDoc.QuerySelector("div")!;

            // 解析到 container 下
            var fragment = _parser.ParseFragment(htmlBlock, container).ToArray();
            foreach (var node in fragment)
                container.AppendChild(node);

            NormalizeWhitespace(container);
            WrapTextNodes(container, keys);

            // 序列化并保持每段末尾换行
            using var sw = new StringWriter();
            foreach (var child in container.ChildNodes)
                child.ToHtml(sw, formatter);
            output.AppendLine(sw.ToString());
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
                child.ReplaceWith(newNode!);
            }
            else
            {
                WrapTextNodes(child, keys);
            }
        }
    }
}