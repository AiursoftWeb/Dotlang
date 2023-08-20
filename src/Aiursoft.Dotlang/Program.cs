using Aiursoft.Dotlang.Core.Framework;
using Aiursoft.Dotlang.BingTranslate;
using System.CommandLine;
using System.Reflection;
using Aiursoft.CommandFramework.Extensions;

var descriptionAttribute = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description;

var program = new RootCommand(descriptionAttribute ?? "Unknown usage.")
    .AddGlobalOptions()
    .AddPlugins(
        new BingTranslatePlugin()
    );

return await program.InvokeAsync(args);
