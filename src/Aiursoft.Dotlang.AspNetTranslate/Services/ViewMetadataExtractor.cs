using System.Text.RegularExpressions;

namespace Aiursoft.Dotlang.AspNetTranslate.Services;

/// <summary>
/// A service to extract localizable string values from [RenderInNavBar] attributes and PageTitle properties in C# files.
/// </summary>
public class ViewMetadataExtractor
{
    // This regex is designed to find string values in [RenderInNavBar(...)] attributes.
    private static readonly Regex RenderInNavBarRegex = new(@"(?:NavGroupName|CascadedLinksGroupName|LinkText)\s*=\s*""((?:\\.|[^""\\])*)""", RegexOptions.Compiled);

    // This regex is designed to find string values assigned to PageTitle property.
    private static readonly Regex PageTitleRegex = new(@"PageTitle\s*=\s*""((?:\\.|[^""\\])*)""", RegexOptions.Compiled);

    /// <summary>
    /// Extracts all distinct strings from [RenderInNavBar] attributes and PageTitle assignments.
    /// </summary>
    /// <param name="csharpContent">The C# source code content.</param>
    /// <returns>A list of distinct keys found.</returns>
    public List<string> ExtractKeys(string csharpContent)
    {
        if (string.IsNullOrWhiteSpace(csharpContent))
        {
            return new List<string>();
        }

        var keys = new List<string>();

        // Extract RenderInNavBar keys
        keys.AddRange(RenderInNavBarRegex.Matches(csharpContent)
            .Select(match => match.Groups[1].Value));

        // Extract PageTitle keys
        keys.AddRange(PageTitleRegex.Matches(csharpContent)
            .Select(match => match.Groups[1].Value));

        return keys
            .Where(k => k.Length > 0)
            .Distinct()
            .ToList();
    }
}
