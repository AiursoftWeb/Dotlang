using Aiursoft.Dotlang.Shared;

namespace Aiursoft.Dotlang.Tests;

[TestClass]
public class MarkdownShredderTests
{
    private readonly MarkdownShredder _shredder = new();

    // ── Helper ────────────────────────────────────────────────────────────

    private static void AssertRoundTrip(string content, List<MarkdownChunk> result)
    {
        var reassembled = string.Concat(result.Select(c => c.Content));
        Assert.AreEqual(content, reassembled,
            "Shred → Reassemble must be lossless");
    }

    // ── Basic functionality ──────────────────────────────────────────────

    [TestMethod]
    public void TestSimpleShred()
    {
        var content = "Hello\n\nWorld";
        var result = _shredder.Shred(content, 100);
        AssertRoundTrip(content, result);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(ChunkType.Translatable, result[0].Type);
        Assert.AreEqual(content, result[0].Content);
    }

    [TestMethod]
    public void TestEmptyContent()
    {
        var result = _shredder.Shred("", 100);
        Assert.AreEqual(0, result.Count);

        result = _shredder.Shred(null!, 100);
        Assert.AreEqual(0, result.Count);
    }

    // ── Code blocks ──────────────────────────────────────────────────────

    [TestMethod]
    public void TestShredWithCodeBlock()
    {
        var content = "Hello\n\n```csharp\nvar a = 1;\n```\n\nWorld";
        var result = _shredder.Shred(content, 100);
        AssertRoundTrip(content, result);

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(ChunkType.Translatable, result[0].Type);
        Assert.AreEqual("Hello\n\n", result[0].Content);

        Assert.AreEqual(ChunkType.Static, result[1].Type);
        Assert.IsTrue(result[1].Content.Contains("var a = 1;"));

        Assert.AreEqual(ChunkType.Translatable, result[2].Type);
        Assert.AreEqual("\n\nWorld", result[2].Content);
    }

    [TestMethod]
    public void TestShredWithTitledCodeBlock()
    {
        var content = "Before\n\n```bash title=\"Install\"\nsudo apt install\n```\n\nAfter";
        var result = _shredder.Shred(content, 100);
        AssertRoundTrip(content, result);

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(ChunkType.Static, result[1].Type);
        Assert.AreEqual("```bash title=\"Install\"\nsudo apt install\n```", result[1].Content);
    }

    [TestMethod]
    public void TestShredWithTildeCodeBlock()
    {
        var content = "Before\n\n~~~bash\nsudo apt install\n~~~\n\nAfter";
        var result = _shredder.Shred(content, 100);
        AssertRoundTrip(content, result);

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(ChunkType.Static, result[1].Type);
        Assert.AreEqual("~~~bash\nsudo apt install\n~~~", result[1].Content);
    }

    [TestMethod]
    public void TestInterleavedCodeBlocks()
    {
        var content = "Para 1\n\n```csharp\ncode1\n```\n\nPara 2\n\n```javascript\ncode2\n```\n\nPara 3";
        var result = _shredder.Shred(content);
        AssertRoundTrip(content, result);

        Assert.AreEqual(5, result.Count);
        Assert.AreEqual("Para 1\n\n", result[0].Content);
        Assert.AreEqual("```csharp\ncode1\n```", result[1].Content);
        Assert.AreEqual("\n\nPara 2\n\n", result[2].Content);
        Assert.AreEqual("```javascript\ncode2\n```", result[3].Content);
        Assert.AreEqual("\n\nPara 3", result[4].Content);
    }

    [TestMethod]
    public void TestShredWithCrlfInterleavedCodeBlocks()
    {
        var crlf = "\r\n";
        var lf = "\n";
        var content = "Para 1" + crlf + crlf +
                      "```xml" + crlf + "<Project Sdk=\"Aiursoft.Apkg.Sdk\">" + crlf + "  <PropertyGroup>" + crlf + "    <PackageName>my-package</PackageName>" + crlf + "  </PropertyGroup>" + crlf + "</Project>" + crlf + "```" + crlf + crlf +
                      "**Cross-Architecture Native Builds**: supports multiple architectures." + crlf + crlf +
                      "```bash" + crlf + "dotnet tool install --global Aiursoft.Apkg.Client" + crlf + "```" + crlf + crlf +
                      "Para 3";

        var result = _shredder.Shred(content);
        // Round-trip against LF-normalized content
        var normalized = content.Replace("\r\n", "\n");
        AssertRoundTrip(normalized, result);

        Assert.AreEqual(5, result.Count, "Should produce exactly 5 chunks: 3 translatable + 2 code blocks");

        Assert.AreEqual(ChunkType.Translatable, result[0].Type);
        Assert.AreEqual("Para 1" + lf + lf, result[0].Content);

        Assert.AreEqual(ChunkType.Static, result[1].Type);
        Assert.IsTrue(result[1].Content.StartsWith("```xml"));

        Assert.AreEqual(ChunkType.Translatable, result[2].Type);
        Assert.IsTrue(result[2].Content.Contains("Cross-Architecture"));

        Assert.AreEqual(ChunkType.Static, result[3].Type);
        Assert.IsTrue(result[3].Content.StartsWith("```bash"));

        Assert.AreEqual(ChunkType.Translatable, result[4].Type);
        Assert.AreEqual(lf + lf + "Para 3", result[4].Content);

        // Verify all code fences preserved
        var allContent = string.Concat(result.Select(c => c.Content));
        var fenceCount = allContent.Split("```").Length - 1;
        Assert.AreEqual(4, fenceCount,
            "Should have exactly 4 triple-backtick fences (2 opening + 2 closing)");
    }

    // ── Greedy merging ───────────────────────────────────────────────────

    [TestMethod]
    public void TestGreedyShred()
    {
        // P1 (2) + \n\nP2 (4) = 6 chars — fits in maxLength 9.
        // \n\nP3 (4) would push it to 10 — triggers flush.
        // Gaps attach to the *following* mergeable block, so:
        //   chunk 1 = P1 + \n\nP2             = "P1\n\nP2"       (6 chars)
        //   chunk 2 = \n\nP3 + \n\nP4        = "\n\nP3\n\nP4"   (8 chars)
        var content = "P1\n\nP2\n\nP3\n\nP4";
        var result = _shredder.Shred(content, 9);
        AssertRoundTrip(content, result);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("P1\n\nP2", result[0].Content);
        Assert.AreEqual("\n\nP3\n\nP4", result[1].Content);
    }

    [TestMethod]
    public void TestHugeParagraph()
    {
        var content = new string('a', 2000);
        var result = _shredder.Shred(content);
        AssertRoundTrip(content, result);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(content, result[0].Content);
        Assert.AreEqual(ChunkType.Translatable, result[0].Type);
    }

    // ── Headings ─────────────────────────────────────────────────────────

    [TestMethod]
    public void TestHeadingIsIndependent()
    {
        var content = "# Title\n\nParagraph text.";
        var result = _shredder.Shred(content, 100);
        AssertRoundTrip(content, result);

        var translatable = result.Where(c => c.Type == ChunkType.Translatable).ToList();
        Assert.AreEqual(2, translatable.Count,
            "Heading and paragraph should be independent chunks");
        Assert.AreEqual("# Title", translatable[0].Content);
        // The paragraph content may include a leading \n\n (the gap between
        // the heading and the paragraph). Only assert the text is present.
        Assert.IsTrue(translatable[1].Content.Contains("Paragraph text."),
            $"Expected paragraph text, got: '{translatable[1].Content}'");
    }

    [TestMethod]
    public void TestMultipleHeadingsAreIndependent()
    {
        var content = "# H1\n\nText one.\n\n## H2\n\nText two.";
        var result = _shredder.Shred(content, 100);
        AssertRoundTrip(content, result);

        var translatable = result.Where(c => c.Type == ChunkType.Translatable).ToList();
        Assert.AreEqual(4, translatable.Count,
            "Each heading and paragraph should be independent");
    }

    // ── Lists ────────────────────────────────────────────────────────────

    [TestMethod]
    public void TestListItemsAreIndependentEach()
    {
        var content = "* Alpha\n* Beta\n* Gamma";
        var result = _shredder.Shred(content, 100);
        AssertRoundTrip(content, result);

        var translatable = result.Where(c => c.Type == ChunkType.Translatable).ToList();
        Assert.AreEqual(3, translatable.Count,
            "Each bullet item should be its own chunk");
        Assert.IsTrue(translatable[0].Content.Contains("Alpha"));
        Assert.IsTrue(translatable[1].Content.Contains("Beta"));
        Assert.IsTrue(translatable[2].Content.Contains("Gamma"));
    }

    [TestMethod]
    public void TestBulletListWithContinuationLines()
    {
        var content = "* **First:** This item\n  continues on next line.\n* **Second:** Another item.";
        var result = _shredder.Shred(content);
        AssertRoundTrip(content, result);

        var translatable = result.Where(c => c.Type == ChunkType.Translatable).ToList();
        Assert.AreEqual(2, translatable.Count);
        Assert.IsTrue(translatable[0].Content.Contains("continues on next line"));
        Assert.IsTrue(translatable[1].Content.Contains("Another item"));
    }

    [TestMethod]
    public void TestOrderedListItemsAreIndependent()
    {
        var content = "1. First item.\n2. Second item.\n3. Third item.";
        var result = _shredder.Shred(content, 100);
        AssertRoundTrip(content, result);

        var translatable = result.Where(c => c.Type == ChunkType.Translatable).ToList();
        Assert.AreEqual(3, translatable.Count);
    }

    [TestMethod]
    public void TestListWithHeadingBefore()
    {
        var content = "## Section\n\n* Item one\n* Item two\n* Item three";
        var result = _shredder.Shred(content, 500);
        AssertRoundTrip(content, result);

        var translatable = result.Where(c => c.Type == ChunkType.Translatable).ToList();
        // Heading + 3 list items = 4 independent chunks
        Assert.AreEqual(4, translatable.Count);
        Assert.AreEqual("## Section", translatable[0].Content);
    }

    [TestMethod]
    public void TestChangelogStyleBulletList()
    {
        var content = "## v2.0.1\n\n* **Offline LLM:** Added `anduinos-why-ai`.\n* **Memory:** Added `swapcontrol`.\n* **OOBE:** Added `anduinos-oobe`.\n* **Network:** Added audit page.\n* **Windows:** Added Exe Launcher.\n* **Xbox:** Added controller driver.\n* **Desktop:** Added taskbar layout.\n* **Core:** Cleaned up Ubuntu messages from `/etc/update-motd.d/` (the login MOTD now shows pure AnduinOS information).";

        var result = _shredder.Shred(content, maxLength: 500);
        AssertRoundTrip(content, result);

        var translatable = result.Where(c => c.Type == ChunkType.Translatable).ToList();
        // 1 heading + 8 bullets = 9 independent translatable chunks
        Assert.IsTrue(translatable.Count >= 9,
            $"Expected at least 9 translatable chunks (1 heading + 8 bullets), got {translatable.Count}");

        // Inline code is NOT split out — it stays within the translatable chunk
        var allTranslatableText = string.Concat(translatable.Select(t => t.Content));
        Assert.IsTrue(allTranslatableText.Contains("`anduinos-why-ai`"),
            "Inline code should be preserved within translatable chunks");
        Assert.IsTrue(allTranslatableText.Contains("AnduinOS information"),
            "Long bullet content should be complete, not truncated");
    }

    // ── Blockquotes ──────────────────────────────────────────────────────

    [TestMethod]
    public void TestBlockquoteIsIndependent()
    {
        var content = "Para.\n\n> This is a quote.\n> Second line of quote.\n\nAfter.";
        var result = _shredder.Shred(content, 500);
        AssertRoundTrip(content, result);

        var translatable = result.Where(c => c.Type == ChunkType.Translatable).ToList();
        Assert.IsTrue(translatable.Any(t => t.Content.Contains("This is a quote")),
            "Blockquote content should be preserved");
    }

    // ── Tables ───────────────────────────────────────────────────────────

    [TestMethod]
    public void TestTableIsPreserved()
    {
        var content = "Before.\n\n| Col A | Col B |\n|-------|-------|\n| a     | b     |\n\nAfter.";
        var result = _shredder.Shred(content, 500);
        AssertRoundTrip(content, result);

        var allText = string.Concat(result.Select(c => c.Content));
        Assert.IsTrue(allText.Contains("| Col A | Col B |"),
            "Table content should be preserved");
    }

    // ── Inline code ──────────────────────────────────────────────────────

    [TestMethod]
    public void TestInlineCodeStaysInParagraph()
    {
        // Inline code is NOT split into separate Static chunks.
        // The whole paragraph goes to the LLM as one Translatable chunk.
        var content = "Run `dotnet run` to start the app.";
        var result = _shredder.Shred(content);
        AssertRoundTrip(content, result);

        Assert.AreEqual(1, result.Count,
            "Inline code should NOT be split out — whole paragraph is one chunk");
        Assert.AreEqual(ChunkType.Translatable, result[0].Type);
        Assert.IsTrue(result[0].Content.Contains("`dotnet run`"),
            "Inline code should be preserved within the translatable chunk");
    }

    [TestMethod]
    public void TestMultipleInlineCodeStaysInParagraph()
    {
        var content = "Install `pkg-a` and `pkg-b` via apt.";
        var result = _shredder.Shred(content);
        AssertRoundTrip(content, result);

        Assert.AreEqual(1, result.Count,
            "Multiple inline codes should all stay in the same paragraph chunk");
        Assert.IsTrue(result[0].Content.Contains("`pkg-a`"));
        Assert.IsTrue(result[0].Content.Contains("`pkg-b`"));
    }

    // ── Horizontal rules ─────────────────────────────────────────────────

    [TestMethod]
    public void TestHorizontalRuleIsStatic()
    {
        var content = "Para 1\n\n---\n\nPara 2";
        var result = _shredder.Shred(content, 100);
        AssertRoundTrip(content, result);

        var statics = result.Where(c => c.Type == ChunkType.Static).ToList();
        Assert.IsTrue(statics.Any(s => s.Content.Contains("---")),
            "Horizontal rule should be a Static chunk");
    }

    // ── Round-trip integrity ─────────────────────────────────────────────

    [TestMethod]
    public void TestRoundTrip_SimpleBullets()
    {
        var content = "* Alpha\n* Beta\n* Gamma";
        var result = _shredder.Shred(content);
        AssertRoundTrip(content, result);
    }

    [TestMethod]
    public void TestRoundTrip_MixedContent()
    {
        var content = "# Title\n\nSome text.\n\n* **Bold:** First `code`.\n* **Item:** Second.\n\n```bash\necho hello\n```\n\nParagraph after.";
        var result = _shredder.Shred(content);
        AssertRoundTrip(content, result);
    }

    [TestMethod]
    public void TestRoundTrip_ConsecutiveBulletsWithInlineCode()
    {
        var content = "## v2.0.1\n\n* **A:** Added `pkg-a`.\n* **B:** Added `pkg-b`.\n* **C:** Fixed `pkg-c`.";
        var result = _shredder.Shred(content);
        AssertRoundTrip(content, result);
    }

    [TestMethod]
    public void TestRoundTrip_BlockquoteWithFormatting()
    {
        var content = "> **Note:** This is `important`.\n> Second line.\n\nNormal.";
        var result = _shredder.Shred(content);
        AssertRoundTrip(content, result);
    }

    // ── Structural guarantees ────────────────────────────────────────────

    [TestMethod]
    public void TestNoSingleChunkContainsMultipleBullets()
    {
        var content = "* Item 1: with some longer text here.\n* Item 2: more long text for testing.\n* Item 3: even more text content.";
        var result = _shredder.Shred(content, maxLength: 1000);

        foreach (var chunk in result.Where(c => c.Type == ChunkType.Translatable))
        {
            var bulletCount = chunk.Content.Split('\n')
                .Count(line => line.TrimStart().StartsWith('*') || line.TrimStart().StartsWith('-'));
            Assert.IsTrue(bulletCount <= 1,
                $"Each translatable chunk should contain at most 1 bullet item. Got {bulletCount}:\n{chunk.Content}");
        }
    }

    [TestMethod]
    public void TestMaxChunkSizeRespected()
    {
        // GreedyMerge groups mergeable paragraphs up to maxLength, but it
        // never splits a single block. If any individual block exceeds
        // maxLength, it is emitted as-is.
        //
        // This test uses all-short paragraphs so every chunk stays within
        // maxLength (no single-paragraph overshoot).
        var content = "P1\n\nP2\n\nP3\n\nP4\n\nP5\n\nP6";
        var maxLength = 10;
        var result = _shredder.Shred(content, maxLength);

        foreach (var chunk in result.Where(c => c.Type == ChunkType.Translatable))
        {
            Assert.IsTrue(chunk.Content.Length <= maxLength,
                $"Translatable chunk exceeds maxLength. Length={chunk.Content.Length}, maxLength={maxLength}");
        }
    }

    // ── MkDocs admonitions ───────────────────────────────────────────────
    // When 4-space indented text follows a !!! or ??? admonition header,
    // Markdig classifies it as CodeBlock. We must re-classify it as
    // Translatable because it is NOT code — it is the admonition body.

    [TestMethod]
    public void TestAdmonitionBodyIsTranslatable()
    {
        // When a blank line separates the admonition header from its indented
        // body, Markdig splits them into two blocks. The indented body is
        // classified as CodeBlock — but it is NOT code, it is translatable
        // admonition body text.
        var content = "!!! note \"Title\"\n\n    This is the admonition body.";
        var result = _shredder.Shred(content);
        AssertRoundTrip(content, result);

        var translatable = result.Where(c => c.Type == ChunkType.Translatable).ToList();
        var bodyChunk = translatable.FirstOrDefault(c => c.Content.Contains("admonition body"));
        Assert.IsNotNull(bodyChunk,
            "Admonition body text after blank line should be in a Translatable chunk, not Static");
    }

    [TestMethod]
    public void TestAdmonitionBodyRoundTrip()
    {
        var content = "!!! warning \"Compat\"\n\n    Due to fragmentation.\n    Second line.";
        var result = _shredder.Shred(content);
        AssertRoundTrip(content, result);
    }

    [TestMethod]
    public void TestStandaloneIndentedCodeStillStatic()
    {
        // Indented code that is NOT preceded by an admonition header
        // must remain Static (regression test).
        var content = "Normal para.\n\n    var x = 1;\n    var y = 2;";
        var result = _shredder.Shred(content);
        AssertRoundTrip(content, result);

        var codeChunk = result.FirstOrDefault(c =>
            c.Content.Contains("var x = 1"));
        Assert.IsNotNull(codeChunk, "Indented code block should still exist in output");
        Assert.AreEqual(ChunkType.Static, codeChunk!.Type,
            "Standalone indented code (no admonition header) must remain Static");
    }

    [TestMethod]
    public void TestCollapsibleAdmonitionBodyIsTranslatable()
    {
        var content = "??? note \"Collapsible\"\n\n    Hidden body text.";
        var result = _shredder.Shred(content);
        AssertRoundTrip(content, result);

        var translatable = result.Where(c => c.Type == ChunkType.Translatable).ToList();
        var bodyChunk = translatable.FirstOrDefault(c => c.Content.Contains("Hidden body"));
        Assert.IsNotNull(bodyChunk,
            "Collapsible admonition (???) body should also be Translatable");
    }

    [TestMethod]
    public void TestAdmonitionWithMultipleBodyLines()
    {
        var content = "!!! tip\n\n    First line.\n    Second line.\n    Third line.";
        var result = _shredder.Shred(content);
        AssertRoundTrip(content, result);

        var translatable = result.Where(c => c.Type == ChunkType.Translatable).ToList();
        var bodyChunk = translatable.FirstOrDefault(c =>
            c.Content.Contains("First line") && c.Content.Contains("Second line"));
        Assert.IsNotNull(bodyChunk, "Multi-line admonition body should be one Translatable chunk");
    }
}
