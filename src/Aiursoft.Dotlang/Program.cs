using Aiursoft.Dotlang.Core.Framework;
using Aiursoft.Dotlang.BingTranslate;
using Aiursoft.CommandFramework;
using Aiursoft.CommandFramework.Extensions;

return await new AiursoftCommand()
    .Configure(command =>
    {
        command
            .AddGlobalOptions()
            .AddPlugins(
                new BingTranslatePlugin()
            );
    })
    .RunAsync(args);
    