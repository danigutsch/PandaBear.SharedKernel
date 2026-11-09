namespace PandaBear.Secrets.Tool.Tests;

/// <summary>Tests that output escapes control characters.</summary>
[Trait("Capability", "Secrets")]
public sealed class EscapedOutputTests
{
    [Fact]
    public async Task Control_characters_are_escaped_and_ordinary_text_is_kept()
    {
        // Arrange
        using var writer = new StringWriter();

        // Act
        await EscapedOutput.Line(writer, "a\u001b[31mred\nforged: line café", TestContext.Current.CancellationToken);

        // Assert
        writer.ToString().ShouldBe("a\\u001B[31mred\\u000Aforged: line café" + writer.NewLine);
    }

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
