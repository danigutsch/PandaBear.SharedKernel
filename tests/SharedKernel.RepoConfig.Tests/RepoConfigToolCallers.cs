using System.CommandLine.Parsing;
using System.Text.RegularExpressions;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Finds <c>sharedkernel-repo</c> command lines in the repository workflows.</summary>
internal static partial class RepoConfigToolCallers
{
    /// <summary>Returns every tool invocation found in the <c>.github/workflows</c> YAML files under <paramref name="root"/>, with its arguments split into tokens.</summary>
    /// <param name="root">The repository root that contains <c>.github/workflows</c>.</param>
    public static IEnumerable<RepoConfigToolCaller> Find(string root)
    {
        // Documentation-only changes skip these tests, so only files whose changes run them are read.
        var workflows = Directory.EnumerateFiles(Path.Combine(root, ".github", "workflows"), "*.yml")
            .Concat(Directory.EnumerateFiles(Path.Combine(root, ".github", "workflows"), "*.yaml"))
            .Order(StringComparer.Ordinal);
        foreach (var file in workflows)
        {
            var relativePath = Path.GetRelativePath(root, file).Replace('\\', '/');
            var text = File.ReadAllText(file).Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\\\n", " ", StringComparison.Ordinal);
            foreach (Match match in Invocation().Matches(text))
            {
                yield return new RepoConfigToolCaller(relativePath, [.. CommandLineParser.SplitCommandLine(match.Groups["arguments"].Value)]);
            }
        }
    }

    // A source-built run passes the arguments after "--"; an installed tool takes them after its command name.
    [GeneratedRegex(@"(?:tools/SharedKernel\.RepoConfig\.Tool[^\n`]*? --|(?<![\w.-])sharedkernel-repo(?: --)?) (?<arguments>[^\n`]*)", RegexOptions.CultureInvariant)]
    private static partial Regex Invocation();
}
