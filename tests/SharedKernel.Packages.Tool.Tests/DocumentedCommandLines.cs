using System.CommandLine.Parsing;

namespace PandaBear.Packages.Tool.Tests;

/// <summary>Finds the tool command lines that the tool README documents.</summary>
internal static class DocumentedCommandLines
{
    /// <summary>Reads the tool README from the repository that contains the test output directory.</summary>
    public static string ReadToolReadme() =>
        File.ReadAllText(Path.Combine(RepositoryRoot.Locate("SharedKernel.slnx"), "tools", "SharedKernel.Packages.Tool", "README.md"));

    /// <summary>
    /// Returns the arguments of every line and inline code span that runs the tool, as <c>dotnet syspackages</c> or through
    /// <c>dotnet tool run syspackages --</c>, without a trailing comment.
    /// </summary>
    public static string[] Find(string markdown) =>
        [.. ReadmeCommandLines.Find(markdown, "dotnet syspackages"), .. ReadmeCommandLines.Find(markdown, "dotnet tool run syspackages --")];

    /// <summary>Splits a documented command line.</summary>
    public static string[] Arguments(string commandLine) => [.. CommandLineParser.SplitCommandLine(commandLine)];
}
