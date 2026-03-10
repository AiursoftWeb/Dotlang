using Aiursoft.Dotlang.Shared;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Aiursoft.Canon;
using Aiursoft.GptClient.Services;

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
}
