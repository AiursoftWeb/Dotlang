using System.Text.RegularExpressions;

namespace Aiursoft.Dotlang.AspNetTranslate.Services;

/// <summary>
/// A service to extract localizable keys from DataAnnotation attributes in C# source code.
/// </summary>
public class DataAnnotationKeyExtractor
{
    // This regex is designed to find `Name = "..."` or `ErrorMessage = "..."` patterns within attributes.
    // It correctly handles escaped quotes (`\"`) inside the string.
    // Breakdown:
    // - `(?:Name|ErrorMessage)` : A non-capturing group that matches either "Name" or "ErrorMessage".
    // - `\s*=\s*`               : Matches the equals sign, allowing for optional whitespace.
    // - `"`                     : Matches the opening double quote.
    // - `(`                     : Starts capturing group 1 (this is the value we want).
    // - `(?:\\.|[^"\\])*`      : Matches the content inside the quotes, handling escaped characters.
    // - `)`                     : Ends capturing group 1.
    // - `"`                     : Matches the closing double quote.
    private static readonly Regex AnnotationRegex = new(@"(?:Name|ErrorMessage)\s*=\s*""((?:\\.|[^""\\])*)""", RegexOptions.Compiled);

    /// <summary>
    /// Extracts all distinct strings from `Name` and `ErrorMessage` properties of DataAnnotation attributes.
    /// </summary>
    /// <param name="csharpContent">The C# source code content.</param>
    /// <returns>A list of distinct keys found.</returns>
    public List<string> ExtractKeys(string csharpContent)
    {
        if (string.IsNullOrWhiteSpace(csharpContent))
        {
            return new List<string>();
        }

        return AnnotationRegex.Matches(csharpContent)
            .Select(match => match.Groups[1].Value) // Get the content of the first capturing group
            .Where(k => k.Length > 0)
            .Distinct()
            .ToList();
    }
}
