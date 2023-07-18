using Aiursoft.Dotlang.Core.Framework;

namespace Aiursoft.Dotlang.Core.Abstracts;

public interface IDotlangPlugin
{
    public CommandHandler[] Install();
}
