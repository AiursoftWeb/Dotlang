# ASP.NET Core App Translator

[![MIT licensed](https://img.shields.io/badge/license-MIT-blue.svg)](https://gitlab.aiursoft.cn/aiursoft/dotlang/-/blob/master/LICENSE)
[![Pipeline stat](https://gitlab.aiursoft.cn/aiursoft/dotlang/badges/master/pipeline.svg)](https://gitlab.aiursoft.cn/aiursoft/dotlang/-/pipelines)
[![Test Coverage](https://gitlab.aiursoft.cn/aiursoft/dotlang/badges/master/coverage.svg)](https://gitlab.aiursoft.cn/aiursoft/dotlang/-/pipelines)
[![NuGet version (Aiursoft.DotLang)](https://img.shields.io/nuget/v/Aiursoft.Dotlang.svg)](https://www.nuget.org/packages/Aiursoft.Dotlang/)
[![ManHours](https://manhours.aiursoft.cn/r/gitlab.aiursoft.cn/aiursoft/dotlang.svg)](https://gitlab.aiursoft.cn/aiursoft/dotlang/-/commits/master?ref_type=heads)

This app helps you generate translated `.cshtml` files and `resources` files. Based on the [Ollama](https://ollama.com/) AI model, it translates the content inside `@Localizer[""]` tags in your ASP.NET Core project.

## Installation

Requirements:

1. [.NET 9 SDK](http://dot.net/)

Run the following command to install this tool:

```bash
dotnet tool install --global Aiursoft.Dotlang
```

## Usage

After getting the binary, run it directly in the terminal.

```cmd
dotlang translate-aspnet --path ~/Code --instance http://ollama:11434/api/chat --model "qwen:32b" --token "your-ollama-token"
Description:
  The command to start translation on an ASP.NET Core project.

Usage:
  dotlang translate-aspnet [options]

Options:
  -v, --verbose                           Show detailed log
  -d, --dry-run                           Preview changes without actually making them
  -p, --path <path> (REQUIRED)            Path of the videos to be parsed.
  -l, --languages <languages> (REQUIRED)  The target languages code. Connect with ','. For example: zh_CN,en_US,ja_JP [default: 
                                          zh-CN,zh-TW,zh-HK,ja-JP,ko-KR,vi-VN,th-TH,de-DE,fr-FR,es-ES,ru-RU,it-IT,pt-PT,pt-BR,ar-SA,nl-NL,sv-SE,pl-PL,tr-TR]
  --instance <instance> (REQUIRED)        The Ollama instance to use.
  --model <model> (REQUIRED)              The Ollama model to use.
  --token <token> (REQUIRED)              The Ollama token to use.
  -?, -h, --help                          Show help and usage information
```

## Run locally

Requirements about how to run

1. [.NET 9 SDK](http://dot.net/)
2. Ollama.
2. Execute `dotnet run` to run the app

## Run in Microsoft Visual Studio

1. Open the `.sln` file in the project path.
2. Press `F5`.

## How does this work?

* Find all files ends with `.cshtml`.
* foreach `cshtml` file, replace all text in tag surrounded with `@Localizer[""]` with the content inside.
* Call ollama to translate all the content. (The entire file will be sent to ollama to make sure AI understands the context.)
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

## Caution

Running this under your project folder may ruin your project! It may change your `cshtml`! Do run `git commit` under your project before running this app.

## How to contribute

There are many ways to contribute to the project: logging bugs, submitting pull requests, reporting issues, and creating suggestions.

Even if you with push rights on the repository, you should create a personal fork and create feature branches there when you need them. This keeps the main repository clean and your workflow cruft out of sight.

We're also interested in your feedback on the future of this project. You can submit a suggestion or feature request through the issue tracker. To make this process more effective, we're asking that these include more information to help define them more clearly.
