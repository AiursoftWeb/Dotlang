# ASP.NET Core App Translator

This app helps you generate translated `.cshtml` files and `resources` files.

## How does it works

* Find all files ends with `.cshtml`
* foreach `cshtml` file, replace all text in tag sround with `@Localizer[""]`
* Call bing translate API to translate all those content
* Save the translated file as `Resource` file in the `Resources` folder.

The Core Translator won't override any existing translation nor resources files. If your content was already surrounded with `@Localizer[""]`, we won't touch it.

## Before running the translator

* Follow the document here [ASP.NET Core Localization](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/localization?view=aspnetcore-2.1)

Use the following code to register the localizer service:

```csharp
// In StartUp.cs ConfigureServices method:
services.AddLocalization(options => options.ResourcesPath = "Resources");

services.AddMvc()
    .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();
```

Use the following code to add localizer middleware:

```csharp
// In StartUp.cs Configure method
var SupportedCultures = new CultureInfo[]
{
    new CultureInfo("en"),
    new CultureInfo("zh")
};
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(defaultLanguage),
    SupportedCultures = SupportedCultures,
    SupportedUICultures = SupportedCultures
});
```

Use the following code to inject localizer:

```cshtml
@using Microsoft.AspNetCore.Mvc.Localization
@inject IViewLocalizer Localizer
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
```

Now run this app!

## How to use

* Install [.NET Core SDK](http://dot.net)
* Compile the project using command `dotnet publich -c Release -r win-x64`
* Run the published `exe` in your ASP.NET Core app root folder
    - Under your app root folder, there shall be `Views` folder and `Models` folder.
* Input your bing translate API key. Get it [here](https://portal.azure.com)

## Caution

Running this under your project folder may ruin your project! It may change your `cshtml`! Do run `git commit` under your project before running this app.
