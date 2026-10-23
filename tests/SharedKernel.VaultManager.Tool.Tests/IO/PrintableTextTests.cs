using PandaBear.VaultManager.Tool.IO;

namespace PandaBear.VaultManager.Tool.Tests.IO;

public sealed class PrintableTextTests
{
    [Theory]
    [InlineData("Bank\u001b[2J", "Bank\uFFFD[2J")]
    [InlineData("Bank\u009b31m", "Bank\uFFFD31m")]
    [InlineData("Bank\u007f\u0000\u0007", "Bank\uFFFD\uFFFD\uFFFD")]
    [InlineData("two\r\nlines\tand a tab", "two  lines and a tab")]
    public void OneLine_replaces_every_control_character(string text, string expected)
    {
        // Act
        string printable = PrintableText.OneLine(text);

        // Assert
        printable.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Bank\u2028# Injected heading", "Bank # Injected heading")]
    [InlineData("Bank\u2029# Injected heading", "Bank # Injected heading")]
    public void OneLine_turns_line_and_paragraph_separators_into_spaces(string text, string expected)
    {
        // Act
        string printable = PrintableText.OneLine(text);

        // Assert
        printable.ShouldBe(expected);
    }

    [Theory]
    [InlineData("invoice\u202Etxt.exe", "invoice\uFFFDtxt.exe")]
    [InlineData("\u2066Bank\u2069", "\uFFFDBank\uFFFD")]
    [InlineData("a\u202Ab\u202Bc\u202Cd\u202De\u2067f\u2068g", "a\uFFFDb\uFFFDc\uFFFDd\uFFFDe\uFFFDf\uFFFDg")]
    public void OneLine_replaces_bidirectional_embeddings_overrides_and_isolates(string text, string expected)
    {
        // Act
        string printable = PrintableText.OneLine(text);

        // Assert
        printable.ShouldBe(expected);
    }

    [Theory]
    [InlineData(0x00)]
    [InlineData(0x07)]
    [InlineData(0x1B)]
    [InlineData(0x1F)]
    [InlineData(0x7F)]
    [InlineData(0x80)]
    [InlineData(0x85)]
    [InlineData(0x9B)]
    [InlineData(0x9F)]
    public void OneLine_replaces_each_control_character_other_than_a_line_break_or_tab(int controlCharacter)
    {
        // Arrange
        string text = string.Concat("before", (char)controlCharacter, "after");

        // Act
        string printable = PrintableText.OneLine(text);

        // Assert
        printable.ShouldBe(string.Concat("before", PrintableText.Replacement, "after"));
    }

    [Theory]
    [InlineData(0x09)]
    [InlineData(0x0A)]
    [InlineData(0x0D)]
    public void OneLine_shows_a_line_break_or_tab_as_a_space(int character)
    {
        // Arrange
        string text = string.Concat("before", (char)character, "after");

        // Act
        string printable = PrintableText.OneLine(text);

        // Assert
        printable.ShouldBe("before after");
    }

    [Theory]
    [InlineData("Bank | Visa ü €")]
    [InlineData("Bob's Café Bank \u00A0€ 😀")]
    [InlineData("\u05D1\u05E0\u05E7 \u200FBank\u200E")]
    public void OneLine_returns_text_without_control_characters_as_it_is(string text)
    {
        // Act
        string printable = PrintableText.OneLine(text);

        // Assert
        printable.ShouldBeSameAs(text);
    }

    [Fact]
    public void TableCell_also_escapes_the_cell_separator()
    {
        // Act
        string cell = PrintableText.TableCell("a|b\u001b\u2028c");

        // Assert
        cell.ShouldBe("a\\|b\uFFFD c");
    }
}
