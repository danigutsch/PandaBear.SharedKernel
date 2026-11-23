using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Parsing;

namespace PandaBear.PlaywrightMcp.Tool.Tests;

/// <summary>Finds and classifies the tool command lines that the tool README documents.</summary>
internal static class DocumentedCommandLines
{
    /// <summary>Reads the tool README from the repository that contains the test output directory.</summary>
    public static string ReadToolReadme() =>
        File.ReadAllText(Path.Combine(RepositoryRoot.Locate("SharedKernel.slnx"), "tools", "SharedKernel.PlaywrightMcp.Tool", "README.md"));

    /// <summary>Returns the arguments of every line and inline code span that starts with the tool invocation, <c>dotnet browsermcp</c>, without a trailing comment.</summary>
    public static string[] Find(string markdown) => ReadmeCommandLines.Find(markdown, "dotnet browsermcp");

    /// <summary>Splits a documented command line.</summary>
    public static string[] Arguments(string commandLine) => [.. CommandLineParser.SplitCommandLine(commandLine)];

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
}
