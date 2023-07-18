using Aiursoft.Dotlang.Core.Abstracts;
using Aiursoft.Dotlang.Core.Framework;

namespace Aiursoft.Dotlang.BingTranslate;

public class BingTranslatePlugin : IDotlangPlugin
{
    public CommandHandler[] Install() => new CommandHandler[] { new TranslateHandler() };
}
