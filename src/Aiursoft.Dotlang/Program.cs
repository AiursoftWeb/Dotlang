﻿using Aiursoft.CommandFramework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.Dotlang.AspNetTranslate;
using Aiursoft.Dotlang.FolderTranslate;

return await new NestedCommandApp()
    .WithGlobalOptions(CommonOptionsProvider.DryRunOption)
    .WithGlobalOptions(CommonOptionsProvider.VerboseOption)
    .WithFeature(new GenerateResxHandler())
    .WithFeature(new WrapCodeHandler())
    .WithFeature(new FolderTranslateHandler())
    .RunAsync(args);
