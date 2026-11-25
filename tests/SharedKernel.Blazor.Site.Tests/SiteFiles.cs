using System.Text.RegularExpressions;

namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Locates the package sources that the stylesheet tests read.</summary>
internal static partial class SiteFiles
{
    /// <summary>Gets the directory of the package project.</summary>
    /// <returns>The absolute directory.</returns>
    public static string ProjectDirectory() =>
        Path.Combine(Testing.RepositoryRoot.Locate("SharedKernel.slnx"), "src", "SharedKernel", "SharedKernel.Blazor.Site");

    /// <summary>Gets the stylesheet path.</summary>
    /// <returns>The absolute path.</returns>
    public static string StylesheetPath() => Path.Combine(ProjectDirectory(), "wwwroot", "site.css");

    /// <summary>Gets the package README path.</summary>
    /// <returns>The absolute path.</returns>
    public static string ReadmePath() => Path.Combine(ProjectDirectory(), "README.md");

    /// <summary>Reads the colour tokens, each defined once as <c>light-dark(#light, #dark)</c>, for one colour scheme.</summary>
    /// <param name="dark">Whether to read the dark side of each pair.</param>
    /// <returns>The hex value by token name without the <c>--site-</c> prefix.</returns>
    public static Dictionary<string, string> Colors(bool dark) => ColorPattern().Matches(File.ReadAllText(StylesheetPath()))
        .ToDictionary(match => match.Groups[1].Value, match => match.Groups[dark ? 3 : 2].Value, StringComparer.Ordinal);

    /// <summary>Returns the declarations of the rule whose selector list starts a line with <paramref name="selector" />.</summary>
    /// <param name="css">The stylesheet.</param>
    /// <param name="selector">The first selector of the rule.</param>
    /// <returns>The text between the braces.</returns>
    public static string Rule(string css, string selector)
    {
        var match = Regex.Match(css, @"^[ \t]*" + Regex.Escape(selector) + @"\s*(?:,[^{]*)?\{([^}]*)\}", RegexOptions.Multiline, TimeSpan.FromSeconds(1));
        return match.Success ? match.Groups[1].Value : throw new InvalidOperationException("No rule starts with " + selector + ".");
    }

    /// <summary>
    /// Reads every rule of the stylesheet, those inside an at-rule included, with its selectors split at the top-level commas and
    /// stripped of comments.
    /// </summary>
    /// <param name="css">The stylesheet.</param>
    /// <returns>The rules in source order.</returns>
    public static IReadOnlyList<CssRule> Rules(string css) => [.. RulePattern().Matches(CommentPattern().Replace(css, string.Empty))
        .Where(match => match.Groups["selectors"].Value.TrimStart() is not ['@', ..])
        .Select(match => new CssRule(
            [.. SelectorSeparator().Split(match.Groups["selectors"].Value).Select(selector => selector.Trim()).Where(selector => selector.Length > 0)],
            match.Groups["body"].Value,
            match.Index))];

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex CommentPattern();

    [GeneratedRegex(@"(?<selectors>[^{}]+)\{(?<body>[^{}]*)\}")]
    private static partial Regex RulePattern();

    [GeneratedRegex(@",(?![^(]*\))")]
    private static partial Regex SelectorSeparator();

    [GeneratedRegex(@"--site-(color-[a-z-]+)\s*:\s*light-dark\(\s*(#[0-9a-fA-F]{6})\s*,\s*(#[0-9a-fA-F]{6})\s*\)\s*;")]
    private static partial Regex ColorPattern();
}
