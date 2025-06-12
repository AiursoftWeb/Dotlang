using Aiursoft.CommandFramework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.Dotlang.AspNetTranslate;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aiursoft.Dotlang.Tests;

[TestClass]
public class IntegrationTests
{
    private readonly SingleCommandApp<GenerateResxHandler> _program = new SingleCommandApp<GenerateResxHandler>()
        .WithDefaultOption(CommonOptionsProvider.PathOptions);

    [TestMethod]
    public async Task InvokeHelp()
    {
        var result = await _program.TestRunAsync(["--help"]);
        Assert.AreEqual(0, result.ProgramReturn);
    }

    [TestMethod]
    public async Task InvokeVersion()
    {
        var result = await _program.TestRunAsync(["--version"]);
        Assert.AreEqual(0, result.ProgramReturn);
    }

    [TestMethod]
    public async Task InvokeUnknown()
    {
        var result = await _program.TestRunAsync(["--wtf"]);
        Assert.AreEqual(1, result.ProgramReturn);
    }

    [TestMethod]
    public async Task InvokeWithoutArg()
    {
        var result = await _program.TestRunAsync([]);
        Assert.AreEqual(1, result.ProgramReturn);
    }

    [TestMethod]
    public async Task InvokeTranslateWithoutArg()
    {
        var result = await _program.TestRunAsync(["translate"]);
        Assert.AreEqual(1, result.ProgramReturn);
    }
}
