using System.Text.RegularExpressions;

namespace PandaBear.Blazor.Site.Tests;

/// <summary>Locates the package sources that the stylesheet tests read.</summary>
internal static partial class SiteFiles
{
    private const string DarkMedia = "prefers-color-scheme: dark";

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

    /// <summary>Reads the hex colour tokens of one colour scheme.</summary>
    /// <param name="dark">Whether to read the dark scheme, merged over the default one.</param>
    /// <returns>The hex value by token name without the <c>--site-</c> prefix.</returns>
    public static Dictionary<string, string> Colors(bool dark)
    {
        var css = File.ReadAllText(StylesheetPath());
        var split = css.IndexOf(DarkMedia, StringComparison.Ordinal);
        var colors = ReadColors(css[..split]);
        if (dark)
        {
            foreach (var entry in ReadColors(css[split..]))
            {
                colors[entry.Key] = entry.Value;
            }
        }

        return colors;
    }

    private static Dictionary<string, string> ReadColors(string css) => ColorPattern().Matches(css)
        .ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value, StringComparer.Ordinal);

    [GeneratedRegex(@"--site-(color-[a-z-]+)\s*:\s*(#[0-9a-fA-F]{6})\s*;")]
    private static partial Regex ColorPattern();
}
