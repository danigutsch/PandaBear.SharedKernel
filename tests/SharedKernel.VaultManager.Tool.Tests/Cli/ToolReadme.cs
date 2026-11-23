using System.CommandLine.Parsing;
using PandaBear.Testing;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

/// <summary>Reads the tool README from the repository the tests were built from.</summary>
internal static class ToolReadme
{
    /// <summary>Returns the text of the tool README in the repository that holds the test output directory.</summary>
    public static string Text() =>
        File.ReadAllText(Path.Combine(RepositoryRoot.Locate("SharedKernel.slnx"), "tools", "SharedKernel.VaultManager.Tool", "README.md"));

    /// <summary>
    /// Returns the arguments of every <c>dotnet vault</c> command line in the README, in file order. A command line is a
    /// line, or an inline code span, that starts with the invocation.
    /// </summary>
    public static IReadOnlyList<string[]> CommandLines() =>
    [
        .. ReadmeCommandLines.Find(Text(), "dotnet vault")
            .Select(static commandLine => CommandLineParser.SplitCommandLine(commandLine).ToArray()),
    ];
}
