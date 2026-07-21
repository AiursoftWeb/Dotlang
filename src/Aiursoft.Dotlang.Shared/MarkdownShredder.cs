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

    public List<MarkdownChunk> Shred(string content, int maxLength = 1000)
    {
        if (string.IsNullOrEmpty(content))
            return [];

        // Normalize line endings to \n so all span calculations are consistent.
        content = content.Replace("\r\n", "\n");

        var document = Markdown.Parse(content, Pipeline);

        var rawChunks = new List<RawChunk>();
        var lastEnd = 0;

        foreach (var block in document)
        {
            // Capture any gap between the previous block end and this block start.
            var gap = block.Span.Start > lastEnd
                ? content[lastEnd..block.Span.Start]
                : null;

            // ListBlock: iterate child items individually so each bullet is its own chunk.
            if (block is ListBlock listBlock)
            {
                // Emit the leading gap as independent Static (list breaks merge chain).
                if (gap != null)
                {
                    rawChunks.Add(new RawChunk(ChunkType.Static, gap, false));
                    lastEnd = block.Span.Start;
                }

                var hasItems = false;
                foreach (var child in listBlock)
                {
                    if (child is ListItemBlock listItem)
                    {
                        hasItems = true;
                        // Emit inter-item gaps (the \n between bullets) as Static.
                        if (listItem.Span.Start > lastEnd)
                        {
                            rawChunks.Add(new RawChunk(ChunkType.Static,
                                content[lastEnd..listItem.Span.Start], false));
                        }

                        var text = content[listItem.Span.Start..(listItem.Span.End + 1)];
                        rawChunks.Add(new RawChunk(ChunkType.Translatable, text, false));
                        lastEnd = listItem.Span.End + 1;
                    }
                }

                // Defensive: skip an empty list block (shouldn't happen in
                // practice, but ensures lastEnd advances so subsequent gaps
                // aren't miscalculated).
                if (!hasItems)
                {
                    lastEnd = block.Span.End + 1;
                }

                continue;
            }

            // ── Gap routing ───────────────────────────────────────────────
            // Whitespace between two Markdown blocks (e.g. the "\n\n" between
            // two paragraphs) is NOT part of either block's Span. We route it
            // to one of three destinations:
            //
            //   gap before current block:
            //   ┌─ current is mergeable paragraph → ATTACH to current block
            //   │    Example: Para1  \n\n  Para2
            //   │    Para2 gets "\n\nPara2" → GreedyMerge combines with Para1
            //   │
            //   ├─ current is Static or non-mergeable AND gap exists
            //   │    ┌─ previous chunk was mergeable → BACKTRACK to previous
            //   │    │   Example: Para1  \n\n  ```code```
            //   │    │   Para1 becomes "Para1\n\n" → stays contiguous
            //   │    │
            //   │    └─ previous was also non-mergeable → gap is ORPHAN
            //   │        Example: # H1  \n\n  # H2
            //   │        gap emitted as Static("\n\n")
            //   │
            //   └─ no gap or leading block → emit normally
            // ───────────────────────────────────────────────────────────

            var (chunkType, mergeable) = ClassifyBlock(block);

            if (chunkType == ChunkType.Translatable && mergeable)
            {
                // Mergeable paragraph: attach the preceding gap so it flows
                // into the greedy-merge buffer instead of interrupting it.
                rawChunks.Add(new RawChunk(chunkType,
                    (gap ?? "") + content[block.Span.Start..(block.Span.End + 1)], true));
            }
            else if (gap != null && rawChunks.Count > 0)
            {
                // Static / non-mergeable block: attempt to attach the gap to the
                // *previous* mergeable chunk so it doesn't break the merge chain.
                var last = rawChunks[^1];
                if (last.Type == ChunkType.Translatable && last.Mergeable)
                {
                    rawChunks[^1] = last with { Content = last.Content + gap };
                }
                else
                {
                    rawChunks.Add(new RawChunk(ChunkType.Static, gap, false));
                }

                rawChunks.Add(new RawChunk(chunkType,
                    content[block.Span.Start..(block.Span.End + 1)], mergeable));
            }
            else
            {
                // Leading block or no gap.
                if (gap != null)
                {
                    rawChunks.Add(new RawChunk(ChunkType.Static, gap, false));
                }

                rawChunks.Add(new RawChunk(chunkType,
                    content[block.Span.Start..(block.Span.End + 1)], mergeable));
            }

            lastEnd = block.Span.End + 1;
        }

        // Emit any trailing whitespace after the last block.
        if (lastEnd < content.Length)
        {
            rawChunks.Add(new RawChunk(ChunkType.Static, content[lastEnd..], false));
        }

        return GreedyMerge(rawChunks, maxLength);
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
            // QuoteBlock and any unlisted block type default to
            // non-mergeable Translatable.
            _ => (ChunkType.Translatable, false),
        };
    }

    private static List<MarkdownChunk> GreedyMerge(List<RawChunk> chunks, int maxLength)
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

            // Mergeable Translatable: add to buffer, flushing if it would exceed maxLength.
            if (buffer.Length + chunk.Content.Length > maxLength && buffer.Length > 0)
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
