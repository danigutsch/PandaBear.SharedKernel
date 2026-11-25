namespace SharedKernel.Secrets.Tests;

/// <summary>Tests that output escapes control characters.</summary>
[Trait(TestTraitNames.CapabilityName, "Secrets")]
public sealed class EscapedOutputTests
{
    [Fact]
    public async Task Control_characters_are_escaped_and_ordinary_text_is_kept()
    {
        // Arrange
        using var writer = new InvariantStringWriter();

        // Act
        await EscapedOutput.Line(writer, "a\u001b[31mred\nforged: line café", TestContext.Current.CancellationToken);

        // Assert
        writer.ToString().ShouldBe("a\\u001B[31mred\\u000Aforged: line café" + writer.NewLine);
    }
}
