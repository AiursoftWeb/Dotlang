using Aiursoft.Dotlang.BingTranslate;
using Aiursoft.CommandFramework;

var command = new TranslateHandler().BuildAsCommand();

return await new AiursoftCommandApp(command)
    .RunAsync(args);
    