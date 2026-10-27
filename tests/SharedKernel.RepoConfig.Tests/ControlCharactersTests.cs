using PandaBear.Tooling;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Tests escaping of control characters for terminal output.</summary>
public sealed class ControlCharactersTests
{
    /// <summary>Verifies that escaping keeps readable Unicode text and escapes terminal control characters.</summary>
    [Theory]
    [InlineData("", "")]
    [InlineData("São Paulo 日本語 😀", "São Paulo 日本語 😀")]
    [InlineData("before\0\t\n\r\u001B\u007Fafter", "before\\u0000\\u0009\\u000A\\u000D\\u001B\\u007Fafter")]
    [InlineData("before\u00AD\u202Eafter", "before\\u00AD\\u202Eafter")]
    [InlineData("before\u2028\u2029after", "before\\u2028\\u2029after")]
    [InlineData("before\U000E0001after", "before\\U000E0001after")]
    [InlineData("before\n😀after", "before\\u000A😀after")]
    public void Escape_keeps_readable_unicode_and_escapes_terminal_controls(string value, string expected)
    {
        // Act
        var escaped = ControlCharacters.Escape(value);

        // Assert
        escaped.ShouldBe(expected);
    }

    /// <summary>Verifies that escaping renders lone surrogate code units as escapes without throwing.</summary>
    [Fact]
    public void Escape_escapes_lone_surrogate_code_units_without_losing_surrounding_text()
    {
        // Act
        var escaped = ControlCharacters.Escape("before\uD800middle\uDC00after");

        // Assert
        escaped.ShouldBe("before\\uD800middle\\uDC00after");
    }
}
