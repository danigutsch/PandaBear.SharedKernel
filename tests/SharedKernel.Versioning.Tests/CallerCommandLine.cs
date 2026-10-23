using System.CommandLine.Parsing;

namespace PandaBear.Versioning.Tests;

internal static class CallerCommandLine
{
    private const string VersioningProject = "--project tools/SharedKernel.Versioning.Tool ";

    // Sample values for shell variables that options parse as versions.
    private static readonly (string Placeholder, string Value)[] Samples =
    [
        ("\"$PACKAGE_VERSION\"", "1.3.0-alpha.7.1"),
    ];

    public static string[] ReadWorkflowCommandLines(string workflowPath) =>
        [.. File.ReadAllLines(workflowPath)
            .Where(static line => line.Contains(VersioningProject, StringComparison.Ordinal))
            .Select(static line => line[(line.IndexOf(" -- ", StringComparison.Ordinal) + " -- ".Length)..].Trim())];

    public static string[] Split(string commandLine)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        var resolved = commandLine;
        foreach (var (placeholder, value) in Samples)
        {
            resolved = resolved.Replace(placeholder, value, StringComparison.Ordinal);
        }

        return [.. CommandLineParser.SplitCommandLine(resolved)];
    }
}
