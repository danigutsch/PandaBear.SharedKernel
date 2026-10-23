using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using System.Text.RegularExpressions;

namespace PandaBear.PlaywrightMcp.Tool.Tests;

/// <summary>Finds and classifies the tool command lines that the tool README documents.</summary>
internal static partial class DocumentedCommandLines
{
    private const string ToolCommandName = "sharedkernel-playwright-mcp";

    /// <summary>Reads the tool README from the repository that contains the test output directory.</summary>
    public static string ReadToolReadme()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SharedKernel.slnx")))
            {
                return File.ReadAllText(Path.Combine(directory.FullName, "tools", "SharedKernel.PlaywrightMcp.Tool", "README.md"));
            }
        }

        throw new InvalidOperationException("SharedKernel.slnx was not found above " + AppContext.BaseDirectory);
    }

    /// <summary>Returns every line and inline code span that starts with the tool command, without a trailing comment.</summary>
    public static string[] Find(string markdown) =>
    [
        .. markdown
            .Split('\n')
            .Select(static line => line.Trim())
            .SelectMany(static line => InlineCode().Matches(line).Select(static match => match.Groups["code"].Value).Prepend(line))
            .Where(static text => text.StartsWith(ToolCommandName + " ", StringComparison.Ordinal))
            .Select(static text => TrailingComment().Replace(text, string.Empty)),
    ];

    /// <summary>Splits a documented command line and drops the tool command.</summary>
    public static string[] Arguments(string commandLine) => [.. CommandLineParser.SplitCommandLine(commandLine).Skip(1)];

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

    [GeneratedRegex("`(?<code>[^`]+)`", RegexOptions.CultureInvariant)]
    private static partial Regex InlineCode();

    [GeneratedRegex(@"\s+#.*$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingComment();
}
