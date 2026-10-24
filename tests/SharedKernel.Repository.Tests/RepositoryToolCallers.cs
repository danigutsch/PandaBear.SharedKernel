using System.CommandLine.Parsing;
using System.Text.RegularExpressions;

namespace PandaBear.Repository.Tests;

/// <summary>Finds CI tool command lines, from source or the installed sharedkernel-ci, in workflows and the tool README.</summary>
internal static partial class RepositoryToolCallers
{
    private const string ArrayExpansion = "${args[@]}";

    public static IEnumerable<RepositoryToolCaller> Find(string root)
    {
        // Documentation-only changes skip these tests, so only files whose changes run them are read.
        var files = Directory.EnumerateFiles(Path.Combine(root, ".github", "workflows"), "*.yml")
            .Append(Path.Combine(root, "tools", "SharedKernel.CI.Tool", "README.md"));
        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(root, file).Replace('\\', '/');
            var text = File.ReadAllText(file).Replace("\\\n", " ", StringComparison.Ordinal);
            string[][] appendedArguments = [.. AppendedArguments().Matches(text).Select(static match => Split(match.Groups["values"].Value))];
            foreach (Match match in Invocation().Matches(text))
            {
                var arguments = Split(match.Groups["arguments"].Value);
                if (!arguments.Contains(ArrayExpansion, StringComparer.Ordinal))
                {
                    yield return new RepositoryToolCaller(relativePath, arguments);
                    continue;
                }

                // A bash array expands to nothing or to the values appended to it.
                yield return new RepositoryToolCaller(relativePath, [.. arguments.Where(static argument => argument != ArrayExpansion)]);
                foreach (var values in appendedArguments)
                {
                    yield return new RepositoryToolCaller(relativePath, [.. arguments.SelectMany(argument => argument == ArrayExpansion ? values : [argument])]);
                }
            }
        }
    }

    private static string[] Split(string commandLine) => [.. CommandLineParser.SplitCommandLine(commandLine)];

    [GeneratedRegex(@"(?:tools/SharedKernel\.CI\.Tool|sharedkernel-ci)[^\n`]*? -- (?<arguments>[^\n`]*)", RegexOptions.CultureInvariant)]
    private static partial Regex Invocation();

    [GeneratedRegex(@"args\+=\((?<values>[^)\n]*)\)", RegexOptions.CultureInvariant)]
    private static partial Regex AppendedArguments();
}
