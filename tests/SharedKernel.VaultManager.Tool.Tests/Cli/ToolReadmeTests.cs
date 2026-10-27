using PandaBear.VaultManager.Tool.Tests.Pipeline;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

/// <summary>Tests that the command lines shown in the tool readme match the real command tree.</summary>
public sealed class ToolReadmeTests
{
    /// <summary>Parsing every command line extracted from the readme against the real command tree yields no parse errors.</summary>
    [Fact]
    public void Every_command_line_in_the_tool_readme_parses_with_the_real_command_tree()
    {
        // Arrange
        var command = VaultCommandLine.Create(new TestPrompts("fixture export password").Environment());

        // Act
        var failures = ToolReadme.CommandLines()
            .Select(args => (Line: string.Join(' ', args), Result: VaultCommandLine.Parse(command, args)))
            .Where(parsed => parsed.Result.Errors.Count > 0)
            .Select(parsed => parsed.Line + " => " + string.Join("; ", parsed.Result.Errors.Select(error => error.Message)))
            .ToArray();

        // Assert
        failures.ShouldBeEmpty();
    }

    /// <summary>The distinct commands that the readme's command lines resolve to include inspect, scan, and merge.</summary>
    [Fact]
    public void Tool_readme_shows_inspect_scan_and_merge()
    {
        // Arrange
        var command = VaultCommandLine.Create(new TestPrompts("fixture export password").Environment());

        // Act
        string[] commandNames = [.. ToolReadme.CommandLines().Select(args => VaultCommandLine.Parse(command, args).CommandResult.Command.Name).Distinct(StringComparer.Ordinal)];

        // Assert
        commandNames.ShouldContain("inspect");
        commandNames.ShouldContain("scan");
        commandNames.ShouldContain("merge");
    }
}
