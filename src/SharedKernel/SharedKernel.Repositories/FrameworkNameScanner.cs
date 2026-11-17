using System.Text.RegularExpressions;

namespace PandaBear.Repositories;

/// <summary>
/// Finds the .NET framework names, such as <c>net11.0</c> or <c>net11.0-windows</c>, in the text of a project, props,
/// workflow, or script file.
/// </summary>
internal static partial class FrameworkNameScanner
{
    /// <summary>
    /// Returns each framework name with its one-based line number, in document order. <c>netstandard</c> and
    /// <c>netcoreapp</c> names are not .NET framework names here, and neither is the right side of a
    /// <c>'$(TargetFramework)' == '…'</c> condition, which selects a version group.
    /// </summary>
    public static IReadOnlyList<(int Line, string Name)> Scan(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        List<(int, string)> found = [];
        for (var index = 0; index < lines.Length; index++)
        {
            foreach (Match match in FrameworkName().Matches(Condition().Replace(lines[index], string.Empty)))
            {
                found.Add((index + 1, match.Value));
            }
        }

        return found;
    }

    [GeneratedRegex(@"'\$\(TargetFramework\)'\s*[=!]=\s*'[^']*'", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Condition();

    [GeneratedRegex(@"\bnet\d+\.\d+(?:-[a-z0-9.]+)?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FrameworkName();
}
