using System.Text.RegularExpressions;

namespace PandaBear.Repositories;

/// <summary>
/// Finds the <c>dotnet run --project</c> commands in the text of a script, workflow, or document.
/// </summary>
internal static partial class DotnetRunScanner
{
    /// <summary>
    /// Returns the commands that name a project they can resolve statically. A line that ends with <c>\</c> continues on
    /// the next line, and so does a line that ends with a backtick when it holds an odd number of backticks, so that
    /// the closing backtick of inline Markdown code does not join lines. A project path that holds <c>$</c>, <c>{</c>,
    /// <c>%</c>, or <c>&lt;</c> is skipped, as is a command without <c>--project</c>.
    /// </summary>
    public static IReadOnlyList<DotnetRunLine> Scan(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        List<DotnetRunLine> found = [];
        for (var index = 0; index < lines.Length; index++)
        {
            var start = index;
            var command = lines[index];
            if (!Run().IsMatch(command))
            {
                continue;
            }

            while (index + 1 < lines.Length && Continues(command))
            {
                index++;
                command = command[..^1] + " " + lines[index];
            }

            var project = Project().Match(command);
            if (!project.Success)
            {
                continue;
            }

            var path = project.Groups["path"].Value.Replace('\\', '/');
            while (path.StartsWith("./", StringComparison.Ordinal))
            {
                path = path[2..];
            }

            if (path.Length > 0 && path.IndexOfAny(['$', '{', '%', '<']) < 0)
            {
                found.Add(new DotnetRunLine(start + 1, path, Framework().IsMatch(command)));
            }
        }

        return found;
    }

    private static bool Continues(string line) =>
        line.EndsWith('\\') || (line.EndsWith('`') && line.Count(static character => character == '`') % 2 == 1);

    [GeneratedRegex(@"\bdotnet\s+run\b", RegexOptions.CultureInvariant)]
    private static partial Regex Run();

    [GeneratedRegex(@"--project(?:\s+|=)(?:""(?<path>[^""]*)""|'(?<path>[^']*)'|(?<path>[^\s`'""]+))", RegexOptions.CultureInvariant)]
    private static partial Regex Project();

    [GeneratedRegex(@"(?<=\s)(?:-f(?=[\s=])|--framework(?=[\s=]|$))", RegexOptions.CultureInvariant)]
    private static partial Regex Framework();
}
