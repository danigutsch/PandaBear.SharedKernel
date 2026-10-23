using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using System.Text.RegularExpressions;

namespace PandaBear.Versioning.Tests;

internal static partial class CallerCommandLine
{
    private const string VersioningProject = "--project tools/SharedKernel.Versioning.Tool ";
    private const string ProjectArgumentSeparator = " -- ";

    private static readonly string[] ToolCommands = ["sharedkernel-version ", "./.tools/sharedkernel-version "];

    // Sample values for shell variables that options parse as versions.
    private static readonly (string Placeholder, string Value)[] Samples =
    [
        ("\"$PACKAGE_VERSION\"", "1.3.0-alpha.7.1"),
    ];

    public static string[] ReadWorkflowCommandLines(string workflowPath) =>
        [.. File.ReadAllLines(workflowPath)
            .Where(static line => line.Contains(VersioningProject, StringComparison.Ordinal))
            .Select(static line => line[(line.IndexOf(ProjectArgumentSeparator, StringComparison.Ordinal) + ProjectArgumentSeparator.Length)..].Trim())];

    /// <summary>
    /// Returns the arguments of every README line that runs the tool, from any segment of a pipeline and without an
    /// input redirect. Lines with <c>&lt;placeholders&gt;</c> describe syntax rather than runnable command lines.
    /// </summary>
    public static string[] ReadReadmeCommandLines(string readmePath) =>
        [.. File.ReadAllLines(readmePath)
            .Select(static line => line.Trim())
            .Where(static line => !Placeholder().IsMatch(line))
            .SelectMany(static line => line.Split('|', StringSplitOptions.TrimEntries))
            .Select(ToolArguments)
            .OfType<string>()
            .Select(static arguments => InputRedirect().Replace(arguments, string.Empty))];

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

    /// <summary>Names what a parse runs: <c>--help</c>, <c>--version</c>, or the command whose own action it selects.</summary>
    public static string Selection(ParseResult parseResult)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        var action = parseResult.Action;
        if (action is HelpAction)
        {
            return "--help";
        }

        if (parseResult.RootCommandResult.Command.Options.OfType<VersionOption>().Any(option => ReferenceEquals(option.Action, action)))
        {
            return "--version";
        }

        var command = parseResult.CommandResult.Command;
        return action is not null && ReferenceEquals(action, command.Action) ? command.Name : "(no command)";
    }

    private static string? ToolArguments(string segment)
    {
        if (segment.Contains(VersioningProject, StringComparison.Ordinal))
        {
            var separator = segment.IndexOf(ProjectArgumentSeparator, StringComparison.Ordinal);
            return separator < 0 ? null : segment[(separator + ProjectArgumentSeparator.Length)..];
        }

        var command = ToolCommands.FirstOrDefault(command => segment.StartsWith(command, StringComparison.Ordinal));
        return command is null ? null : segment[command.Length..];
    }

    [GeneratedRegex(@"<[^<>\s]+>", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"\s+<\s*\S+$", RegexOptions.CultureInvariant)]
    private static partial Regex InputRedirect();
}
