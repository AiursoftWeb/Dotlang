using Aiursoft.CommandFramework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.Dotlang.BingTranslate;

return await new NestedCommandApp()
    .WithGlobalOptions(CommonOptionsProvider.DryRunOption)
    .WithGlobalOptions(CommonOptionsProvider.VerboseOption)
    .WithFeature(new TranslateHandler())
    .RunAsync(args);
