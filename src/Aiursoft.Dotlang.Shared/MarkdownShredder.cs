using System.Text;
using Markdig;
using Markdig.Syntax;

namespace Aiursoft.Dotlang.Shared;

public enum ChunkType
{
    Translatable,
    Static
}

public class MarkdownChunk
{
    public string Content { get; set; } = string.Empty;
    public ChunkType Type { get; set; }

    public override string ToString()
    {
        return $"[{Type}]: {Content.Replace("\n", "\\n")}";
    }
}

public class MarkdownShredder
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .Build();

    public List<MarkdownChunk> Shred(string content, int mergeThreshold = 1000)
    {
        if (string.IsNullOrEmpty(content))
            return [];

        content = content.Replace("\r\n", "\n");

        var document = Markdown.Parse(content, Pipeline);

        var rawChunks = new List<RawChunk>();
        var lastEnd = 0;

        foreach (var block in document)
        {
            ProcessBlock(block, content, rawChunks, ref lastEnd);
        }

        // Emit any trailing whitespace after the last block.
        if (lastEnd < content.Length)
        {
            rawChunks.Add(new RawChunk(ChunkType.Static, content[lastEnd..], false));
        }

        return GreedyMerge(rawChunks, mergeThreshold);
    }

    /// <summary>
    /// Unified recursive block processor. For simple list items (a single
    /// paragraph — the common CHANGELOG case) we skip recursion and treat
    /// them as mergeable paragraphs so they can carpool into fewer chunks.
    /// Complex containers (nested lists, quotes, items with code blocks)
    /// are recursed into so nested structures are correctly classified.
    /// </summary>
    private static void ProcessBlock(Block block, string content, List<RawChunk> rawChunks, ref int lastEnd)
    {
        // ── Container routing ──────────────────────────────────────────
        // Simple list item = one child that is a paragraph. Treat it as a
        // flat mergeable block — it does NOT need recursive dissection.
        bool isSimpleListItem = block is ListItemBlock li && li.Count == 1 && li[0] is ParagraphBlock;

        bool shouldRecurse = block is ContainerBlock && !isSimpleListItem &&
                             (block is ListBlock || block is ListItemBlock || block is QuoteBlock);

        if (shouldRecurse)
        {
            var container = (ContainerBlock)block;
            var cGap = block.Span.Start > lastEnd ? content[lastEnd..block.Span.Start] : null;

            if (cGap != null)
            {
                // Try to attach the gap to the previous mergeable chunk so
                // it doesn't interrupt the merge chain.
                if (rawChunks.Count > 0 && rawChunks[^1].Type == ChunkType.Translatable && rawChunks[^1].Mergeable)
                {
                    var last = rawChunks[^1];
                    rawChunks[^1] = last with { Content = last.Content + cGap };
                }
                else
                {
                    rawChunks.Add(new RawChunk(ChunkType.Static, cGap, false));
                }
                lastEnd = block.Span.Start;
            }

            foreach (var child in container)
            {
                ProcessBlock(child, content, rawChunks, ref lastEnd);
            }

            return;
        }

        // ── Leaf / simple-item routing ────────────────────────────────
        var (chunkType, mergeable) = ClassifyBlock(block);

        // MkDocs admonition body override.
        var isAdmonitionBody = chunkType == ChunkType.Static &&
                               block is CodeBlock and not FencedCodeBlock && IsAdmonitionBody(rawChunks);
        if (isAdmonitionBody)
            chunkType = ChunkType.Translatable;

        var gap = block.Span.Start > lastEnd ? content[lastEnd..block.Span.Start] : null;
        var text = content[block.Span.Start..(block.Span.End + 1)];

        if (chunkType == ChunkType.Translatable && mergeable)
        {
            // Mergeable: attach the preceding gap so it flows into the
            // greedy-merge buffer. For simple list items this is where the
            // "* " bullet marker (living in the gap) joins the text.
            rawChunks.Add(new RawChunk(chunkType, (gap ?? "") + text, true));
        }
        else if (gap != null && rawChunks.Count > 0)
        {
            var last = rawChunks[^1];
            if (last.Type == ChunkType.Translatable && last.Mergeable)
                rawChunks[^1] = last with { Content = last.Content + gap };
            else
                rawChunks.Add(new RawChunk(ChunkType.Static, gap, false));

            AddLeaf();
        }
        else
        {
            if (gap != null)
                rawChunks.Add(new RawChunk(ChunkType.Static, gap, false));

            AddLeaf();
        }

        lastEnd = block.Span.End + 1;

        void AddLeaf()
        {
            if (isAdmonitionBody)
                AddAdmonitionBody(text, rawChunks);
            else
                rawChunks.Add(new RawChunk(chunkType, text, mergeable));
        }
    }

    private static void AddAdmonitionBody(string text, List<RawChunk> chunks)
    {
        // Parse the body at its own indentation level so fenced/indented examples
        // stay static. Map the resulting chunks back to the untouched source.
        var body = new StringBuilder();
        var sourceOffsets = new List<int>();
        for (var i = 0; i < text.Length;)
        {
            var lineStart = i;
            var columns = 0;
            while (i < text.Length && columns < 4 && text[i] is ' ' or '\t')
            {
                columns = text[i++] == '\t' ? 4 : columns + 1;
            }
            if (i == text.Length) break;

            sourceOffsets.Add(lineStart);
            body.Append(text[i++]);
            while (i < text.Length && text[i - 1] != '\n')
            {
                sourceOffsets.Add(i);
                body.Append(text[i++]);
            }
        }
        sourceOffsets.Add(text.Length);

        var offset = 0;
        foreach (var chunk in new MarkdownShredder().Shred(body.ToString()))
        {
            var start = sourceOffsets[offset];
            offset += chunk.Content.Length;
            chunks.Add(new RawChunk(chunk.Type, text[start..sourceOffsets[offset]], false));
        }
    }

    /// <summary>
    /// Checks whether the current block is the indented body of a MkDocs admonition
    /// (!!! or ???). If the most recent Translatable chunk starts with <c>!!!</c> or
    /// <c>???</c>, the current CodeBlock is admonition body, not source code.
    /// </summary>
    private static bool IsAdmonitionBody(List<RawChunk> chunks)
    {
        for (var i = chunks.Count - 1; i >= 0; i--)
        {
            // Gaps may be attached to a header or emitted as separate static chunks.
            // Real static content ends the admonition context, so do not skip code.
            var text = chunks[i].Content.Trim();
            if (text.Length == 0) continue;
            if (chunks[i].Type == ChunkType.Static) return false;
            return text.StartsWith("!!!") || text.StartsWith("???");
        }
        return false;
    }

    private static (ChunkType Type, bool Mergeable) ClassifyBlock(Block block)
    {
        return block switch
        {
            FencedCodeBlock => (ChunkType.Static, false),
            CodeBlock => (ChunkType.Static, false),
            HtmlBlock => (ChunkType.Static, false),
            ThematicBreakBlock => (ChunkType.Static, false),
            HeadingBlock => (ChunkType.Translatable, false),
            ParagraphBlock => (ChunkType.Translatable, true),
            // Simple list items (single paragraph, the common CHANGELOG case)
            // are mergeable so they can carpool — this is what compresses
            // 400 API calls down to ~10.
            ListItemBlock => (ChunkType.Translatable, true),
            _ => (ChunkType.Translatable, false),
        };
    }

    private static List<MarkdownChunk> GreedyMerge(List<RawChunk> chunks, int mergeThreshold)
    {
        var result = new List<MarkdownChunk>();
        var buffer = new StringBuilder();

        foreach (var chunk in chunks)
        {
            if (chunk.Type == ChunkType.Static || !chunk.Mergeable)
            {
                FlushBuffer();
                result.Add(new MarkdownChunk { Content = chunk.Content, Type = chunk.Type });
                continue;
            }

            // Mergeable Translatable: add to buffer, flushing if it would exceed mergeThreshold.
            if (buffer.Length + chunk.Content.Length > mergeThreshold && buffer.Length > 0)
            {
                FlushBuffer();
            }

            buffer.Append(chunk.Content);
        }

        FlushBuffer();
        return result;

        void FlushBuffer()
        {
            if (buffer.Length > 0)
            {
                result.Add(new MarkdownChunk { Content = buffer.ToString(), Type = ChunkType.Translatable });
                buffer.Clear();
            }
        }
    }

    /// <summary>Internal chunk representation with mergeability flag.</summary>
    private readonly record struct RawChunk(ChunkType Type, string Content, bool Mergeable);
}
