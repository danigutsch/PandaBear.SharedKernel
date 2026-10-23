using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using System.Text.RegularExpressions;

namespace PandaBear.Versioning.Tests;

internal static partial class CallerCommandLine
{
    private const string VersioningProject = "--project tools/SharedKernel.Versioning.Tool ";
    private const string ProjectArgumentSeparator = " -- ";
    private const string ToolCommand = "sharedkernel-version ";
    private const string LocalToolRun = "dotnet tool run " + ToolCommand;
    private const string LocalToolRunSeparator = "-- ";

    private static readonly string[] ToolCommands = [ToolCommand, "./.tools/" + ToolCommand];

    // dotnet tool run answers its own help options unless they follow "--"; it passes every other argument to the tool.
    private static readonly string[] LocalToolRunHelpOptions = ["--help", "-h", "-?", "/h", "/?"];

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
    /// Returns the arguments of every README line and inline code span that runs the tool, from any segment of a
    /// pipeline and without an input redirect. Text with <c>&lt;placeholders&gt;</c> is a synopsis, which
    /// <see cref="ReadReadmeSynopses" /> reads.
    /// </summary>
    /// <exception cref="InvalidOperationException">Other text runs or names the tool but is not a runnable command line.</exception>
    public static string[] ReadReadmeCommandLines(string readmePath) =>
        [.. ReadReadmeTexts(readmePath)
            .Where(static text => !Placeholder().IsMatch(text))
            .SelectMany(static text => text.Split('|', StringSplitOptions.TrimEntries))
            .Select(ToolArguments)
            .OfType<string>()
            .Select(static arguments => InputRedirect().Replace(arguments, string.Empty))];

    /// <summary>
    /// Returns the command and option names of every README usage synopsis: a line or inline code span that runs the
    /// tool with <c>&lt;placeholders&gt;</c>, <c>[optional]</c> options, and <c>(a | b)</c> alternatives. A synopsis
    /// whose command is a placeholder names no command.
    /// </summary>
    public static (string Command, string[] Options)[] ReadReadmeSynopses(string readmePath) =>
        [.. ReadReadmeTexts(readmePath)
            .Where(static text => Placeholder().IsMatch(text))
            .Select(static text => ToolCommands.FirstOrDefault(command => text.StartsWith(command, StringComparison.Ordinal)) is { } command
                ? text[command.Length..]
                : null)
            .OfType<string>()
            .Where(static arguments => !arguments.StartsWith('<'))
            .Select(static arguments => (
                arguments.Split(' ', 2)[0],
                OptionName().Matches(arguments).Select(static match => match.Value).ToArray()))];

    /// <summary>
    /// Names each synopsis command that <paramref name="rootCommand" /> lacks, and each <c>command --option</c> that
    /// the command does not accept.
    /// </summary>
    public static string[] FindUnknownOptions(RootCommand rootCommand, IEnumerable<(string Command, string[] Options)> synopses)
    {
        ArgumentNullException.ThrowIfNull(rootCommand);
        ArgumentNullException.ThrowIfNull(synopses);
        return [.. synopses.SelectMany(synopsis =>
            rootCommand.Subcommands.FirstOrDefault(command => command.Name == synopsis.Command) is { } command
                ? synopsis.Options
                    .Where(option => !command.Options.Any(accepted => accepted.Name == option || accepted.Aliases.Contains(option)))
                    .Select(option => synopsis.Command + " " + option)
                : [synopsis.Command])];
    }

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

    // Each line without its inline code spans, followed by the spans.
    private static IEnumerable<string> ReadReadmeTexts(string readmePath) =>
        File.ReadAllLines(readmePath)
            .Select(static line => line.Trim())
            .SelectMany(static line => InlineCode().Matches(line)
                .Select(static match => match.Groups["code"].Value)
                .Prepend(InlineCode().Replace(line, " ")));

    private static string? ToolArguments(string segment)
    {
        if (segment.Contains(VersioningProject, StringComparison.Ordinal))
        {
            var separator = segment.IndexOf(ProjectArgumentSeparator, StringComparison.Ordinal);
            return separator < 0 ? throw NotRunnable(segment) : segment[(separator + ProjectArgumentSeparator.Length)..];
        }

        if (segment.StartsWith(LocalToolRun, StringComparison.Ordinal))
        {
            var arguments = segment[LocalToolRun.Length..];
            if (arguments.StartsWith(LocalToolRunSeparator, StringComparison.Ordinal))
            {
                return arguments[LocalToolRunSeparator.Length..];
            }

            return Split(arguments).Intersect(LocalToolRunHelpOptions, StringComparer.Ordinal).Any() ? throw NotRunnable(segment) : arguments;
        }

        var command = ToolCommands.FirstOrDefault(command => segment.StartsWith(command, StringComparison.Ordinal));
        if (command is not null)
        {
            return segment[command.Length..];
        }

        return segment.Contains(ToolCommand, StringComparison.Ordinal) ? throw NotRunnable(segment) : null;
    }

    private static InvalidOperationException NotRunnable(string text) =>
        new($"README text runs or names the tool but is not a runnable command line: {text}");

    [GeneratedRegex("`(?<code>[^`]+)`", RegexOptions.CultureInvariant)]
    private static partial Regex InlineCode();

    [GeneratedRegex(@"<[^<>\s]+>", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"(?<![\w-])--[a-z][a-z0-9-]*", RegexOptions.CultureInvariant)]
    private static partial Regex OptionName();

    [GeneratedRegex(@"\s+<\s*\S+$", RegexOptions.CultureInvariant)]
    private static partial Regex InputRedirect();
}
