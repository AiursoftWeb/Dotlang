using Aiursoft.CommandFramework;
using Aiursoft.Dotlang.BingTranslate;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dotlang.Tests;

[TestClass]
public class IntegrationTests
{
    private readonly AiursoftCommandApp _program;

    public IntegrationTests()
    {
        var command = new TranslateHandler().BuildAsCommand();
        _program = new AiursoftCommandApp(command);
    }

    [TestMethod]
    public async Task InvokeHelp()
    {
        var result = await _program.TestRunAsync(new[] { "--help" });
        Assert.AreEqual(0, result.ProgramReturn);
    }

    [TestMethod]
    public async Task InvokeVersion()
    {
        var result = await _program.TestRunAsync(new[] { "--version" });
        Assert.AreEqual(0, result.ProgramReturn);
    }

    [TestMethod]
    public async Task InvokeUnknown()
    {
        var result = await _program.TestRunAsync(new[] { "--wtf" });
        Assert.AreEqual(1, result.ProgramReturn);
    }

    [TestMethod]
    public async Task InvokeWithoutArg()
    {
        var result = await _program.TestRunAsync(Array.Empty<string>());
        Assert.AreEqual(1, result.ProgramReturn);
    }

    [TestMethod]
    public async Task InvokeTranslateWithoutArg()
    {
        var result = await _program.TestRunAsync(new[] { "translate" });
        Assert.AreEqual(1, result.ProgramReturn);
    }
}
