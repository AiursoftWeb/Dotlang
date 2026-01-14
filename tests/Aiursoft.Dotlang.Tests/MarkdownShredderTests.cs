using Aiursoft.Dotlang.Shared;

namespace Aiursoft.Dotlang.Tests;

[TestClass]
public class MarkdownShredderTests
{
    private readonly MarkdownShredder _shredder = new();

    [TestMethod]
    public void TestSimpleShred()
    {
        var content = "Hello\n\nWorld";
        var result = _shredder.Shred(content, 100);
        Assert.HasCount(1, result);
        Assert.AreEqual(ChunkType.Translatable, result[0].Type);
        Assert.AreEqual(content, result[0].Content);
    }

    [TestMethod]
    public void TestShredWithCodeBlock()
    {
        var content = "Hello\n\n```csharp\nvar a = 1;\n```\n\nWorld";
        var result = _shredder.Shred(content, 100);
        
        // Parts expected:
        // 1. Hello\n\n (Translatable)
        // 2. ```csharp\nvar a = 1;\n``` (Static)
        // 3. \n\nWorld (Translatable)
        
        Assert.HasCount(3, result);
        Assert.AreEqual(ChunkType.Translatable, result[0].Type);
        Assert.AreEqual("Hello\n\n", result[0].Content);
        
        Assert.AreEqual(ChunkType.Static, result[1].Type);
        Assert.Contains("var a = 1;", result[1].Content);
        
        Assert.AreEqual(ChunkType.Translatable, result[2].Type);
        Assert.AreEqual("\n\nWorld", result[2].Content);
    }

    [TestMethod]
    public void TestGreedyShred()
    {
        var content = "P1\n\nP2\n\nP3\n\nP4";
        // P1\n\nP2 is 6 chars.
        // P1\n\nP2\n\n is 8 chars.
        // P1\n\nP2\n\nP3 is 10 chars.
        
        var result = _shredder.Shred(content, 9);
        
        // Expected:
        // Group 1: P1\n\nP2\n\n (8 chars)
        // Group 2: P3\n\nP4 (6 chars)
        
        Assert.HasCount(2, result);
        Assert.AreEqual("P1\n\nP2\n\n", result[0].Content);
        Assert.AreEqual("P3\n\nP4", result[1].Content);
    }

    [TestMethod]
    public void TestHugeParagraph()
    {
        var content = new string('a', 2000);
        var result = _shredder.Shred(content);
        
        Assert.HasCount(1, result);
        Assert.AreEqual(content, result[0].Content);
        Assert.AreEqual(ChunkType.Translatable, result[0].Type);
    }

    [TestMethod]
    public void TestInterleavedCodeBlocks()
    {
        var content = "Para 1\n\n```csharp\ncode1\n```\n\nPara 2\n\n```javascript\ncode2\n```\n\nPara 3";
        var result = _shredder.Shred(content);
        
        // Expected:
        // 1. Para 1\n\n (Translatable)
        // 2. ```csharp\ncode1\n``` (Static)
        // 3. \n\nPara 2\n\n (Translatable)
        // 4. ```javascript\ncode2\n``` (Static)
        // 5. \n\nPara 3 (Translatable)
        
        Assert.HasCount(5, result);
        Assert.AreEqual("Para 1\n\n", result[0].Content);
        Assert.AreEqual("```csharp\ncode1\n```", result[1].Content);
        Assert.AreEqual("\n\nPara 2\n\n", result[2].Content);
        Assert.AreEqual("```javascript\ncode2\n```", result[3].Content);
        Assert.AreEqual("\n\nPara 3", result[4].Content);
    }
}
