using Aiursoft.CommandFramework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.Dotlang.BingTranslate;
using Aiursoft.Dotlang.OllamaTranslate;

return await new NestedCommandApp()
    .WithGlobalOptions(CommonOptionsProvider.DryRunOption)
    .WithGlobalOptions(CommonOptionsProvider.VerboseOption)
    .WithFeature(new TranslateHandler())
    .WithFeature(new AiTranslateHandler())
    .RunAsync(args);
