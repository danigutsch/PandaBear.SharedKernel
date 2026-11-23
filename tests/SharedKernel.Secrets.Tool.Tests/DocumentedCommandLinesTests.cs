using System.CommandLine.Help;

namespace PandaBear.Secrets.Tool.Tests;

/// <summary>Tests that the tool README documents only command lines that the tool accepts.</summary>
[Trait("Capability", "Secrets")]
public sealed class DocumentedCommandLinesTests
{
    [Fact]
    public void Every_readme_command_line_parses_to_a_command_of_the_tool()
    {
        // Arrange
        var environment = new SecretsEnvironment(Path.GetTempPath(), new ExecutableMap(SecretsToolHarness.ResolveNone), ScriptedToolRunner.Unused(), () => Stream.Null, () => Stream.Null, static _ => null, static () => false);
        var root = SecretsCommandLine.Create(environment);
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
        commandLines.Length.ShouldBeGreaterThan(5);
        failures.ShouldBeEmpty();
    }
}
