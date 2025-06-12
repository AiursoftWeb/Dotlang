namespace Aiursoft.Dotlang.AspNetTranslate.Services;

public class CshtmlLine
{
    public string Raw { get; }
    public CshtmlLineType Type { get; }

    public CshtmlLine(string raw, CshtmlLineType type)
    {
        Raw = raw;
        Type = type;
    }

    public override string ToString() => $"{Type}: {Raw}";
}