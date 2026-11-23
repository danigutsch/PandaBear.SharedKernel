using System.CommandLine.Parsing;

namespace PandaBear.K6.Tool.Tests;

/// <summary>Finds the tool command lines that the tool README documents.</summary>
internal static class DocumentedCommandLines
{
    /// <summary>Reads the tool README from the repository that contains the test output directory.</summary>
    public static string ReadToolReadme() =>
        File.ReadAllText(Path.Combine(RepositoryRoot.Locate("SharedKernel.slnx"), "tools", "SharedKernel.K6.Tool", "README.md"));

    /// <summary>
    /// Returns the arguments of every line and inline code span that runs the tool, as <c>dotnet loadtest</c> or through
    /// <c>dotnet tool run loadtest --</c>, without a trailing comment.
    /// </summary>
    public static string[] Find(string markdown) =>
        [.. ReadmeCommandLines.Find(markdown, "dotnet loadtest"), .. ReadmeCommandLines.Find(markdown, "dotnet tool run loadtest --")];

    /// <summary>Splits a documented command line.</summary>
    public static string[] Arguments(string commandLine) => [.. CommandLineParser.SplitCommandLine(commandLine)];
}
