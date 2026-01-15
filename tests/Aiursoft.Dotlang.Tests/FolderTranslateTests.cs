using Aiursoft.Canon;
using Aiursoft.Dotlang.AspNetTranslate.Services;
using Aiursoft.Dotlang.Shared;
using Aiursoft.GptClient.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aiursoft.Dotlang.Tests;

[TestClass]
public class FolderTranslateTests
{
    private string _tempDir = null!;
    private string _sourceDir = null!;
    private string _destDir = null!;

    [TestInitialize]
    public void Init()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _sourceDir = Path.Combine(_tempDir, "source");
        _destDir = Path.Combine(_tempDir, "dest");
        Directory.CreateDirectory(_sourceDir);
        Directory.CreateDirectory(_destDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [TestMethod]
    public async Task TestSkipExistingFiles()
    {
        // Arrange
        var sourceFile = Path.Combine(_sourceDir, "test.html");
        var destFile = Path.Combine(_destDir, "test.html");
        await File.WriteAllTextAsync(sourceFile, "Source Content");
        await File.WriteAllTextAsync(destFile, "Existing Destination Content");

        var services = new ServiceCollection();
        services.AddLogging(l => l.AddConsole());
        services.AddTransient<FolderFilesTranslateEngine>();
        services.AddTransient<OllamaBasedTranslatorEngine>();
        services.AddTransient<MarkdownShredder>();
        services.AddTransient<ChatClient>();
        services.AddTransient<RetryEngine>();
        services.AddHttpClient();
        services.Configure<TranslateOptions>(o =>
        {
            o.OllamaInstance = "http://localhost:11434"; // Dummy
            o.OllamaModel = "llama3";
            o.OllamaToken = "token";
        });

        var sp = services.BuildServiceProvider();
        var engine = sp.GetRequiredService<FolderFilesTranslateEngine>();

        // Act
        // This should NOT throw exception because it skips the file and thus skips the network call.
        await engine.TranslateAsync(
            sourceFolder: _sourceDir,
            destinationFolder: _destDir,
            language: "zh_CN",
            recursive: false,
            extensions: ["html"],
            skipExistingFiles: true);

        // Assert
        var content = await File.ReadAllTextAsync(destFile);
        Assert.AreEqual("Existing Destination Content", content);
    }
    
    [TestMethod]
    public async Task TestDoNotSkipExistingFiles()
    {
         // Arrange
        var sourceFile = Path.Combine(_sourceDir, "test.html");
        // Destination does not exist, so it should try to translate
        
        await File.WriteAllTextAsync(sourceFile, "Source Content");

        var services = new ServiceCollection();
        services.AddLogging(l => l.AddConsole());
        services.AddTransient<FolderFilesTranslateEngine>();
        services.AddTransient<OllamaBasedTranslatorEngine>();
        services.AddTransient<MarkdownShredder>();
        services.AddTransient<ChatClient>();
        services.AddTransient<RetryEngine>();
        services.AddHttpClient();
        services.Configure<TranslateOptions>(o =>
        {
            o.OllamaInstance = "http://invalid-url-that-does-not-exist:12345"; 
            o.OllamaModel = "llama3";
            o.OllamaToken = "token";
        });

        var sp = services.BuildServiceProvider();
        var engine = sp.GetRequiredService<FolderFilesTranslateEngine>();

        // Act
        // This SHOULD throw exception because it tries to connect to invalid URL
        try
        {
            await engine.TranslateAsync(
                sourceFolder: _sourceDir,
                destinationFolder: _destDir,
                language: "zh_CN",
                recursive: false,
                extensions: ["html"],
                skipExistingFiles: false);
            Assert.Fail("Should have thrown exception");
        }
        catch (Exception)
        {
            // Expected
        }
    }
}