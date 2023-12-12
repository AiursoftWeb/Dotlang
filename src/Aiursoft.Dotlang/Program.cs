using Aiursoft.CommandFramework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.Dotlang.BingTranslate;

return await new SingleCommandApp<TranslateHandler>()
    .WithDefaultOption(CommonOptionsProvider.PathOptions)
    .RunAsync(args);
    