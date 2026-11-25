using System.CommandLine;

namespace SharedKernel.CommandLine.Tests;

/// <summary>Parses arguments with a command that holds only the options under test.</summary>
internal static class CommandParsing
{
    /// <summary>Parses <paramref name="arguments"/> with a <c>test</c> command that holds <paramref name="options"/>.</summary>
    public static ParseResult Parse(IEnumerable<Option> options, params string[] arguments)
    {
        var command = new RootCommand("test");
        foreach (var option in options)
        {
            command.Options.Add(option);
        }

        return command.Parse(arguments);
    }

    /// <summary>Returns the messages of the parse errors of <paramref name="result"/>.</summary>
    public static string[] Errors(ParseResult result) => [.. result.Errors.Select(static error => error.Message)];
}
