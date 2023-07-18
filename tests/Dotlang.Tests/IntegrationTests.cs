using System.CommandLine;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Aiursoft.Dotlang.Core.Framework;
using Aiursoft.Dotlang.BingTranslate;

namespace IDotlangPlugin.Tests;

[TestClass]
public class IntegrationTests
{
    private readonly RootCommand _program;

    public IntegrationTests()
    {
        this._program = new RootCommand("Test command.")
            .AddGlobalOptions()
            .AddPlugins(
                new BingTranslatePlugin()
            );
    }

    [TestMethod]
    public async Task InvokeHelp()
    {
        var result = await _program.InvokeAsync(new[] { "--help" });
        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public async Task InvokeVersion()
    {
        var result = await _program.InvokeAsync(new[] { "--version" });
        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public async Task InvokeUnknown()
    {
        var result = await _program.InvokeAsync(new[] { "--wtf" });
        Assert.AreEqual(1, result);
    }

    [TestMethod]
    public async Task InvokeWithoutArg()
    {
        var result = await _program.InvokeAsync(Array.Empty<string>());
        Assert.AreEqual(1, result);
    }

    [TestMethod]
    public async Task InvokeTranslateWithoutArg()
    {
        var result = await _program.InvokeAsync(new[] { "translate" });
        Assert.AreEqual(1, result);
    }
}
