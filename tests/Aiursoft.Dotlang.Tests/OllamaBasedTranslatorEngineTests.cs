using Aiursoft.Dotlang.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Aiursoft.Canon;
using Aiursoft.GptClient.Services;
using System.Text;
using System.Runtime.CompilerServices;
using Aiursoft.GptClient.Abstractions;

namespace Aiursoft.Dotlang.Tests;

[TestClass]
public class OllamaBasedTranslatorEngineTests
{
    private OllamaBasedTranslatorEngine _engine = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = Options.Create(new TranslateOptions
        {
            OllamaInstance = "http://localhost",
            OllamaModel = "test",
            OllamaToken = "test"
        });
        var retryEngine = new RetryEngine(NullLogger<RetryEngine>.Instance);
        var logger = NullLogger<OllamaBasedTranslatorEngine>.Instance;
        
        using var httpClient = new HttpClient();
        var chatClient = new ChatClient(httpClient, NullLogger<ChatClient>.Instance);
        var shredder = new MarkdownShredder();

        _engine = new OllamaBasedTranslatorEngine(
            options,
            retryEngine,
            logger,
            chatClient,
            shredder);
    }

    [TestMethod]
    [DataRow("``` พร้อมใช้งานทันที", "พร้อมใช้งานทันที")]
    [DataRow("```markdown\nพร้อมใช้งานทันที\n```", "พร้อมใช้งานทันที")]
    [DataRow("พร้อมใช้งานทันที", "พร้อมใช้งานทันที")]
    [DataRow("```พร้อมใช้งานทันที```", "พร้อมใช้งานทันที")]
    [DataRow("Here is the translation: ```พร้อมใช้งานทันที```", "พร้อมใช้งานทันที")]
    [DataRow("```th-TH\nพร้อมใช้งานทันที", "พร้อมใช้งานทันที")]
    [DataRow("```\nพร้อมใช้งานทันที\n```", "พร้อมใช้งานทันที")]
    [DataRow("   ```   พร้อมใช้งานทันที   ```   ", "พร้อมใช้งานทันที")]
    public void TestExtractTranslation(string raw, string expected)
    {
        var result = _engine.ExtractTranslation(raw);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void TestExtractTranslationEmpty()
    {
        try
        {
            _engine.ExtractTranslation("``` ```");
            Assert.Fail("Should have thrown exception");
        }
        catch (InvalidOperationException)
        {
            // Expected
        }
    }

    [TestMethod]
    public void TestExtractTranslationWhitespace()
    {
        try
        {
            _engine.ExtractTranslation("   ");
            Assert.Fail("Should have thrown exception");
        }
        catch (InvalidOperationException)
        {
            // Expected
        }
    }

    [TestMethod]
    public async Task TestTranslateStreamAsync()
    {
        var options = Options.Create(new TranslateOptions
        {
            OllamaInstance = "http://localhost",
            OllamaModel = "test",
            OllamaToken = "test"
        });
        var retryEngine = new RetryEngine(NullLogger<RetryEngine>.Instance);
        var logger = NullLogger<OllamaBasedTranslatorEngine>.Instance;
        
        using var httpClient = new HttpClient();
        var chatClient = new StreamingChatClient(httpClient);
        
        var shredder = new MarkdownShredder();
        var engine = new OllamaBasedTranslatorEngine(
            options,
            retryEngine,
            logger,
            chatClient,
            shredder);

        var result = new StringBuilder();
        await foreach (var part in engine.TranslateStreamAsync("Hello world", "en-US"))
        {
            result.Append(part);
        }

        // Should strip "Here is the translation:", "```markdown", and "```"
        // Also leading/trailing whitespace around the content.
        Assert.AreEqual("Hello world", result.ToString().Trim());
    }
    private sealed class StreamingChatClient(HttpClient httpClient)
        : ChatClient(httpClient, NullLogger<ChatClient>.Instance)
    {
        public override async IAsyncEnumerable<string> AskModelStream(
            OpenAiRequestModel model, string completionApiUrl, string? token,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return "Here is ";
            yield return "the translation:\n";
            yield return "```markdown\n";
            yield return "Hello ";
            yield return "world\n";
            yield return "```";
            await Task.Yield();
        }
    }
}
