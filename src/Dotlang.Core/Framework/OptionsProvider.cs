using Aiursoft.Dotlang.Core.Abstracts;
using System.CommandLine;

namespace Aiursoft.Dotlang.Core.Framework;

public static class OptionsProvider
{
    public static readonly Option<string> PathOptions = new(
        aliases: new[] { "--path", "-p" },
        description: "Path of the videos to be parsed.")
    {
        IsRequired = true
    };

    public static readonly Option<bool> DryRunOption = new(
        aliases: new[] { "--dry-run", "-d" },
        description: "Preview changes without actually making them");

    public static readonly Option<bool> VerboseOption = new(
        aliases: new[] { "--verbose", "-v" },
        description: "Show detailed log");

    private static Option[] GetGlobalOptions()
    {
        return new Option[]
        {
            PathOptions,
            DryRunOption,
            VerboseOption
        };
    }

    public static RootCommand AddGlobalOptions(this RootCommand command)
    {
        var options = GetGlobalOptions();
        foreach (var option in options)
        {
            command.AddGlobalOption(option);
        }
        return command;
    }

    public static RootCommand AddPlugins(this RootCommand command, params IDotlangPlugin[] pluginInstallers)
    {
        foreach (var plugin in pluginInstallers)
        {
            foreach (var pluginFeature in plugin.Install())
            {
                command.Add(pluginFeature.BuildAsCommand());
            }
        }
        return command;
    }
}
