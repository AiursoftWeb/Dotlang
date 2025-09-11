using System.Text.RegularExpressions;

namespace Aiursoft.Dotlang.AspNetTranslate.Services;

/// <summary>
/// A service to extract localizer keys from C# source code.
/// </summary>
public class CSharpKeyExtractor
{
    // This regex is designed to find `localizer["..."]` patterns.
    // It correctly handles escaped quotes (`\"`) inside the string.
    // Breakdown:
    // - `localizer\["` : Matches the literal prefix `localizer["`.
    // - `(`           : Starts capturing group 1 (this is what we want to extract).
    // - `(?:         : Starts a non-capturing group for the content.
    // - `\\.`         : Matches any escaped character (e.g., `\"`).
    // - `|`           : OR
    // - `[^"\\]`      : Matches any character that is NOT a quote or a backslash.
    // - `)*`          : Repeats the non-capturing group zero or more times.
    // - `)`           : Ends capturing group 1.
    // - `"`           : Matches the closing quote.
    // - `\]`          : Matches the closing bracket.
    private static readonly Regex LocalizerRegex = new(@"localizer\[""((?:\\.|[^""\\])*)""\]", RegexOptions.Compiled);

    /// <summary>
    /// Extracts all distinct strings wrapped in `localizer["..."]` from a given C# code string.
    /// </summary>
    /// <param name="csharpContent">The C# source code content.</param>
    /// <returns>A list of distinct keys found.</returns>
    public List<string> ExtractLocalizerKeys(string csharpContent)
    {
        if (string.IsNullOrWhiteSpace(csharpContent))
        {
            return new List<string>();
        }

        return LocalizerRegex.Matches(csharpContent)
            .Select(match => match.Groups[1].Value) // Get the content of the first capturing group
            .Where(k => k.Length > 0)
            .Distinct()
            .ToList();
    }
}
