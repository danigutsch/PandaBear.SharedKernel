namespace PandaBear.Secrets.Tool.Tests;

/// <summary>Tests that the tool escapes control characters in what it prints.</summary>
[Trait(TestTraitNames.CapabilityName, "Secrets")]
public sealed class EscapedOutputCommandTests
{
    [Fact]
    public async Task An_error_message_from_a_tool_is_escaped_in_the_tool_output()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var runner = ScriptedToolRunner.Failing(1, "bad \u001b]0;title\u0007 text");

        // Act
        var run = await SecretsToolHarness.Run(directory.Path, runner, SecretsToolHarness.ResolveAll, null, null, "keygen", "--output", "id.txt", "--force");

        // Assert
        run.Error.ShouldNotContain("\u001b", StringComparison.Ordinal);
        run.Error.ShouldNotContain("\u0007", StringComparison.Ordinal);
    }
}
