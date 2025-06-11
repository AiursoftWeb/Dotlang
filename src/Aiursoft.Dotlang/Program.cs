using Aiursoft.CommandFramework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.Doglang.AspNetTranslate;
using Aiursoft.Dotlang.OllamaTranslate;

return await new NestedCommandApp()
    .WithGlobalOptions(CommonOptionsProvider.DryRunOption)
    .WithGlobalOptions(CommonOptionsProvider.VerboseOption)
    .WithFeature(new AspNetCoreProjectTranslateHandler())
    .WithFeature(new AiTranslateHandler())
    .RunAsync(args);
