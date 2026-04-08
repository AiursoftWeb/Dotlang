using Aiursoft.Dotlang.Shared;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Aiursoft.Canon;
using Aiursoft.GptClient.Services;
using Aiursoft.GptClient.Abstractions;
using System.Text;

namespace Aiursoft.Dotlang.Tests;

[TestClass]
public class OllamaBasedTranslatorEngineTests
{
    private OllamaBasedTranslatorEngine _engine = null!;

    [TestInitialize]
    public void Setup()
    {
        var optionsMock = new Mock<IOptions<TranslateOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new TranslateOptions
        {
            OllamaInstance = "http://localhost",
            OllamaModel = "test",
            OllamaToken = "test"
        });
        var retryEngine = new RetryEngine(new Mock<ILogger<RetryEngine>>().Object);
        var loggerMock = new Mock<ILogger<OllamaBasedTranslatorEngine>>();
        
        var chatClientLoggerMock = new Mock<ILogger<ChatClient>>();
        var chatClientMock = new Mock<ChatClient>(new HttpClient(), chatClientLoggerMock.Object);
        var shredder = new MarkdownShredder();

        _engine = new OllamaBasedTranslatorEngine(
            optionsMock.Object,
            retryEngine,
            loggerMock.Object,
            chatClientMock.Object,
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
        var optionsMock = new Mock<IOptions<TranslateOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new TranslateOptions
        {
            OllamaInstance = "http://localhost",
            OllamaModel = "test",
            OllamaToken = "test"
        });
        var retryEngine = new RetryEngine(new Mock<ILogger<RetryEngine>>().Object);
        var loggerMock = new Mock<ILogger<OllamaBasedTranslatorEngine>>();
        
        var chatClientLoggerMock = new Mock<ILogger<ChatClient>>();
        var chatClientMock = new Mock<ChatClient>(new HttpClient(), chatClientLoggerMock.Object);
        
        // Mock streaming response
        async IAsyncEnumerable<string> MockStream()
        {
            yield return "Here is ";
            yield return "the translation:\n";
            yield return "```markdown\n";
            yield return "Hello ";
            yield return "world\n";
            yield return "```";
            await Task.Yield();
        }

        chatClientMock
            .Setup(c => c.AskModelStream(It.IsAny<OpenAiRequestModel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(MockStream());

        var shredder = new MarkdownShredder();
        var engine = new OllamaBasedTranslatorEngine(
            optionsMock.Object,
            retryEngine,
            loggerMock.Object,
            chatClientMock.Object,
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
}
