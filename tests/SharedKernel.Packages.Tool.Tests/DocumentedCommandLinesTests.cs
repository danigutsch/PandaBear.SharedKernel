using System.CommandLine.Help;

namespace PandaBear.Packages.Tool.Tests;

/// <summary>Tests that the tool README documents only command lines that the tool accepts.</summary>
[Trait("Capability", "Packages")]
public sealed class DocumentedCommandLinesTests
{
    [Fact]
    public void Every_readme_command_line_parses_to_the_plan_command()
    {
        // Arrange
        var root = PackagesCommandLine.Create(Path.GetTempPath(), static _ => null, new RoutingProcessRunner([]));
        var commandLines = DocumentedCommandLines.Find(DocumentedCommandLines.ReadToolReadme());

        // Act
        var failures = commandLines
            .Select(commandLine => (CommandLine: commandLine, Result: root.Parse(DocumentedCommandLines.Arguments(commandLine))))
            .Where(static entry => entry.Result.Errors.Count > 0
                || (entry.Result.Action is not HelpAction
                    && !ReferenceEquals(entry.Result.Action, entry.Result.CommandResult.Command.Action)))
            .Select(static entry => entry.CommandLine + " => " + string.Join("; ", entry.Result.Errors.Select(static error => error.Message)))
            .ToArray();

        // Assert
        commandLines.ShouldNotBeEmpty();
        failures.ShouldBeEmpty();
    }
}
