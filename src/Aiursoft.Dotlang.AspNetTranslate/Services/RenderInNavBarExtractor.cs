using System.Text.RegularExpressions;

namespace Aiursoft.Dotlang.AspNetTranslate.Services;

/// <summary>
/// A service to extract localizable string values from [RenderInNavBar] attributes in C# controller files.
/// </summary>
public class RenderInNavBarExtractor
{
    // This regex is designed to find string values in [RenderInNavBar(...)] attributes.
    // It matches patterns like:
    // - NavGroupName = "Features"
    // - CascadedLinksGroupName = "Dashboard"
    // - LinkText = "Add an agent"
    // - CascadedLinksIcon = "align-left" (but we exclude icons as they are not localized)
    // Breakdown:
    // - `(?:NavGroupName|CascadedLinksGroupName|LinkText)` : Matches property names that need localization
    // - `\s*=\s*`               : Matches the equals sign with optional whitespace
    // - `"`                     : Matches the opening double quote
    // - `(`                     : Starts capturing group 1 (this is the value we want)
    // - `(?:\\.|[^"\\])*`      : Matches the content inside the quotes, handling escaped characters
    // - `)`                     : Ends capturing group 1
    // - `"`                     : Matches the closing double quote
    private static readonly Regex RenderInNavBarRegex = new(@"(?:NavGroupName|CascadedLinksGroupName|LinkText)\s*=\s*""((?:\\.|[^""\\])*)""", RegexOptions.Compiled);

    /// <summary>
    /// Extracts all distinct strings from NavGroupName, CascadedLinksGroupName, and LinkText properties
    /// in [RenderInNavBar] attributes.
    /// </summary>
    /// <param name="csharpContent">The C# source code content.</param>
    /// <returns>A list of distinct keys found.</returns>
    public List<string> ExtractKeys(string csharpContent)
    {
        if (string.IsNullOrWhiteSpace(csharpContent))
        {
            return new List<string>();
        }

        return RenderInNavBarRegex.Matches(csharpContent)
            .Select(match => match.Groups[1].Value) // Get the content of the first capturing group
            .Where(k => k.Length > 0)
            .Distinct()
            .ToList();
    }
}
