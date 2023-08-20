using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.CommandFramework.Framework;

namespace Aiursoft.Dotlang.BingTranslate;

public class BingTranslatePlugin : IPlugin
{
    public CommandHandler[] Install() => new CommandHandler[] { new TranslateHandler() };
}
