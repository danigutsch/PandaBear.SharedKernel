using System.CommandLine.Parsing;

namespace PandaBear.Secrets.Tool.Tests;

/// <summary>Finds the tool command lines that the tool README documents.</summary>
internal static class DocumentedCommandLines
{
    /// <summary>Reads the tool README from the repository that contains the test output directory.</summary>
    public static string ReadToolReadme() =>
        File.ReadAllText(Path.Combine(RepositoryRoot.Locate("SharedKernel.slnx"), "tools", "SharedKernel.Secrets.Tool", "README.md"));

    /// <summary>
    /// Returns the arguments of every line and inline code span that runs the tool, as <c>dotnet secrets</c> or through
    /// <c>dotnet tool run secrets --</c>, without a trailing comment.
    /// </summary>
    public static string[] Find(string markdown) =>
        [.. ReadmeCommandLines.Find(markdown, "dotnet secrets"), .. ReadmeCommandLines.Find(markdown, "dotnet tool run secrets --")];

    /// <summary>Splits a documented command line.</summary>
    public static string[] Arguments(string commandLine) => [.. CommandLineParser.SplitCommandLine(commandLine)];
}
