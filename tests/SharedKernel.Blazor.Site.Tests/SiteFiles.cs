using System.Text.RegularExpressions;

namespace PandaBear.Blazor.Site.Tests;

/// <summary>Locates the package sources that the stylesheet tests read.</summary>
internal static partial class SiteFiles
{
    /// <summary>Gets the directory of the package project.</summary>
    /// <returns>The absolute directory.</returns>
    public static string ProjectDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SharedKernel.slnx")))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new InvalidOperationException("The repository root was not found.")
            : Path.Combine(directory.FullName, "src", "SharedKernel", "SharedKernel.Blazor.Site");
    }

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

    [GeneratedRegex(@"--site-(color-[a-z-]+)\s*:\s*light-dark\(\s*(#[0-9a-fA-F]{6})\s*,\s*(#[0-9a-fA-F]{6})\s*\)\s*;")]
    private static partial Regex ColorPattern();
}
