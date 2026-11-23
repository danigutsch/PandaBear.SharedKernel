using System.CommandLine.Parsing;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Reads the command lines that the tool README documents.</summary>
internal static class DocumentedCommandLines
{
    private const string Invocation = "dotnet repo ";

    /// <summary>Returns the text of the tool README in the repository.</summary>
    public static string ReadToolReadme() =>
        File.ReadAllText(Path.Combine(
            CiTestSelectionTestProcess.GetRepositoryRoot(),
            "tools",
            "SharedKernel.RepoConfig.Tool",
            "README.md"));

    /// <summary>Returns each trimmed line of the Markdown that starts with the tool invocation, <c>dotnet repo</c>.</summary>
    public static IReadOnlyList<string> Find(string markdown) =>
        markdown
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith(Invocation, StringComparison.Ordinal))
            .ToArray();

    /// <summary>Splits a documented command line into arguments, dropping <c>dotnet</c> and the tool name.</summary>
    public static IReadOnlyList<string> Arguments(string commandLine) =>
        CommandLineParser.SplitCommandLine(commandLine).Skip(2).ToArray();
}
