using System.Text;
using System.Text.RegularExpressions;

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
    public List<MarkdownChunk> Shred(string content, int maxLength = 1000)
    {
        var result = new List<MarkdownChunk>();
        if (string.IsNullOrEmpty(content))
        {
            return result;
        }

        // 1. Split by code blocks
        // Using a regex that matches ```...``` including the backticks.
        // It handles the language identifier and content.
        var codeBlockRegex = new Regex(@"(```[a-zA-Z0-9#+]*\n?.*?\n?```)", RegexOptions.Singleline);
        var parts = codeBlockRegex.Split(content);
        var matches = codeBlockRegex.Matches(content);

        int matchIndex = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (string.IsNullOrEmpty(part))
            {
                // If it's a match, it shouldn't be empty (it has at least ``` ```)
                // But Split might produce empty strings if code blocks are adjacent or at start/end.
                if (i % 2 != 0) 
                {
                    // This is a match part
                    result.Add(new MarkdownChunk { Content = matches[matchIndex++].Value, Type = ChunkType.Static });
                }
                continue;
            }

            if (i % 2 == 0)
            {
                // Non-code block part. Further shred it.
                result.AddRange(ShredNonCodePart(part, maxLength));
            }
            else
            {
                // Code block part. Keep as is.
                result.Add(new MarkdownChunk { Content = matches[matchIndex++].Value, Type = ChunkType.Static });
            }
        }

        return result;
    }

    private List<MarkdownChunk> ShredNonCodePart(string content, int maxLength)
    {
        var result = new List<MarkdownChunk>();
        
        // Split by \n\n or \r\n\r\n
        var paragraphSeparatorRegex = new Regex(@"(\n\s*\n|\r\n\s*\r\n)");
        var parts = paragraphSeparatorRegex.Split(content);
        var matches = paragraphSeparatorRegex.Matches(content);

        // parts[0] = P1
        // matches[0] = \n\n
        // parts[1] = P2
        // ...
        
        var currentTranslatable = new StringBuilder();
        int matchIndex = 0;

        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            
            if (i % 2 == 0)
            {
                // This is a paragraph
                if (string.IsNullOrEmpty(part)) continue;

                if (currentTranslatable.Length + part.Length > maxLength && currentTranslatable.Length > 0)
                {
                    // Current translatable is full enough, flush it.
                    result.Add(new MarkdownChunk { Content = currentTranslatable.ToString(), Type = ChunkType.Translatable });
                    currentTranslatable.Clear();
                }

                if (part.Length > maxLength)
                {
                    // Even a single paragraph is too long.
                    // If we have something in currentTranslatable, it was already flushed above.
                    result.Add(new MarkdownChunk { Content = part, Type = ChunkType.Translatable });
                }
                else
                {
                    currentTranslatable.Append(part);
                }
            }
            else
            {
                // This is a separator
                var separator = matches[matchIndex++].Value;
                if (currentTranslatable.Length + separator.Length > maxLength && currentTranslatable.Length > 0)
                {
                    // Flushing before adding separator if adding it would exceed maxLength.
                    // This means the separator will be at the beginning of the next translatable chunk or become static.
                    result.Add(new MarkdownChunk { Content = currentTranslatable.ToString(), Type = ChunkType.Translatable });
                    currentTranslatable.Clear();
                }
                currentTranslatable.Append(separator);
            }
        }

        if (currentTranslatable.Length > 0)
        {
            result.Add(new MarkdownChunk { Content = currentTranslatable.ToString(), Type = ChunkType.Translatable });
        }

        return result;
    }
}
