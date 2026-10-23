using PandaBear.VaultManager.Tool.Tests.Pipeline;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

public sealed class ToolReadmeTests
{
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
