using System.CommandLine.Parsing;

namespace PandaBear.RepoConfig.Tests;

internal static class DocumentedCommandLines
{
    private const string ToolCommandName = "sharedkernel-repo";

    public static string ReadToolReadme() =>
        File.ReadAllText(Path.Combine(
            CiTestSelectionTestProcess.GetRepositoryRoot(),
            "tools",
            "SharedKernel.RepoConfig.Tool",
            "README.md"));

    public static IReadOnlyList<string> Find(string markdown) =>
        markdown
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith(ToolCommandName + " ", StringComparison.Ordinal))
            .ToArray();

    public static IReadOnlyList<string> Arguments(string commandLine) =>
        CommandLineParser.SplitCommandLine(commandLine).Skip(1).ToArray();
}
