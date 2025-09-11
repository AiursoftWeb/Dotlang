using Aiursoft.CommandFramework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.Dotlang.AspNetTranslate;

return await new NestedCommandApp()
    .WithGlobalOptions(CommonOptionsProvider.DryRunOption)
    .WithGlobalOptions(CommonOptionsProvider.VerboseOption)
    .WithFeature(new GenerateResxForViewsHandler())
    .WithFeature(new GenerateResxForCsharpHandler())
    .WithFeature(new GenerateResxForDataAnnotationsHandler())
    .WithFeature(new WrapCodeHandler())
    .WithFeature(new FolderTranslateHandler())
    .RunAsync(args);
