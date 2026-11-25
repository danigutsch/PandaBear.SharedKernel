namespace SharedKernel.EntityFrameworkCore.Tests;

/// <summary>Tests literal SQL LIKE patterns before they are passed to a provider.</summary>
[Trait(TestTraitNames.ScopeName, "unit")]
[Trait(TestTraitNames.CapabilityName, "SqlLikePattern")]
public sealed class SqlLikePatternTests
{
    [Theory]
    [InlineData("%", @"%\%%")]
    [InlineData("_", @"%\_%")]
    [InlineData(@"\", @"%\\%")]
    [InlineData(@"\%_", @"%\\\%\_%")]
    [InlineData(@"A_b%東京\", @"%A\_b\%東京\\%")]
    [InlineData("  ÉTÉ \U0001F600 a\u0301 ", "%  ÉTÉ \U0001F600 a\u0301 %")]
    [InlineData("", "%%")]
    public void A_contains_pattern_escapes_metacharacters_and_preserves_the_other_text(string text, string expected)
    {
        // Arrange & Act
        var pattern = SqlLikePattern.ContainsLiteral(text);

        // Assert
        pattern.ShouldBe(expected);
        SqlLikePattern.EscapeCharacter.ShouldBe("\\");
    }

    [Fact]
    public void A_missing_literal_is_rejected()
    {
        // Arrange
        Action buildPattern = () => SqlLikePattern.ContainsLiteral(null!);

        // Act & Assert
        buildPattern.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe("text");
    }
}
