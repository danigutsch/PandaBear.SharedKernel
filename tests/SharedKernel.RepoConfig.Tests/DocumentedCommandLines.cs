using System.CommandLine.Parsing;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Reads the command lines that the tool README documents.</summary>
internal static class DocumentedCommandLines
{
    /// <summary>Returns the text of the tool README in the repository.</summary>
    public static string ReadToolReadme() =>
        File.ReadAllText(Path.Combine(
            RepositoryRoot.Locate("SharedKernel.slnx"),
            "tools",
            "SharedKernel.RepoConfig.Tool",
            "README.md"));

    /// <summary>Returns the arguments of every line and inline code span of the Markdown that starts with the tool invocation, <c>dotnet repo</c>.</summary>
    public static IReadOnlyList<string> Find(string markdown) => ReadmeCommandLines.Find(markdown, "dotnet repo");

    /// <summary>Splits a documented command line into arguments.</summary>
    public static IReadOnlyList<string> Arguments(string commandLine) =>
        CommandLineParser.SplitCommandLine(commandLine).ToArray();
}
