using Aiursoft.CommandFramework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.Dotlang.BingTranslate;
using Aiursoft.Dotlang.GptTranslate;

return await new NestedCommandApp()
    .WithGlobalOptions(CommonOptionsProvider.DryRunOption)
    .WithGlobalOptions(CommonOptionsProvider.VerboseOption)
    .WithFeature(new TranslateHandler())
    .WithFeature(new GptTranslateHandler())
    .RunAsync(args);
