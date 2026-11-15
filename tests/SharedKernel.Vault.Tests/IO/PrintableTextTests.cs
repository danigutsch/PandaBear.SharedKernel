using PandaBear.Vault.IO;

namespace PandaBear.Vault.Tests.IO;

/// <summary>Tests that text is made safe for printing on one line.</summary>
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
    [InlineData("\u05D1\u05E0\u05E7 Bank \u0628\u0646\u0643")]
    [InlineData("\u9280\u884C \uC740\uD589 \u3042")]
    [InlineData("e\u0301 a\u0308 \u0915\u094D\u0937")]
    [InlineData("\uD83D\uDE00 \uD83D\uDC4D\uD83C\uDFFD \uD835\uDD18 \uD840\uDC0B")]
    [InlineData("\u2764\uFE0F \u845B\uDB40\uDD00 \uE000 \u00A0\u3000")]
    public void OneLine_returns_text_without_control_characters_as_it_is(string text)
    {
        // Act
        string printable = PrintableText.OneLine(text);

        // Assert
        printable.ShouldBeSameAs(text);
    }

    [Theory]
    [InlineData(0x00AD)]
    [InlineData(0x034F)]
    [InlineData(0x0600)]
    [InlineData(0x061C)]
    [InlineData(0x070F)]
    [InlineData(0x115F)]
    [InlineData(0x1160)]
    [InlineData(0x17B4)]
    [InlineData(0x180E)]
    [InlineData(0x200B)]
    [InlineData(0x200C)]
    [InlineData(0x200D)]
    [InlineData(0x200E)]
    [InlineData(0x200F)]
    [InlineData(0x2060)]
    [InlineData(0x2064)]
    [InlineData(0x2065)]
    [InlineData(0x206F)]
    [InlineData(0x3164)]
    [InlineData(0xFEFF)]
    [InlineData(0xFFA0)]
    [InlineData(0xFFF0)]
    [InlineData(0xFFF9)]
    [InlineData(0xFFFB)]
    public void OneLine_replaces_each_invisible_format_character(int formatCharacter)
    {
        // Arrange
        string text = string.Concat("before", PrintableTextSamples.Text(formatCharacter), "after");

        // Act
        string printable = PrintableText.OneLine(text);

        // Assert
        printable.ShouldBe(string.Concat("before", PrintableText.Replacement, "after"));
    }

    [Theory]
    [InlineData(0x110BD)]
    [InlineData(0x1BCA0)]
    [InlineData(0x1D173)]
    [InlineData(0x1D17A)]
    [InlineData(0xE0000)]
    [InlineData(0xE0001)]
    [InlineData(0xE0020)]
    [InlineData(0xE0041)]
    [InlineData(0xE007F)]
    [InlineData(0xE0FFF)]
    public void OneLine_replaces_each_supplementary_format_character_with_one_replacement_character(int formatCharacter)
    {
        // Arrange
        string text = string.Concat("before", PrintableTextSamples.Text(formatCharacter), "after");

        // Act
        string printable = PrintableText.OneLine(text);

        // Assert
        printable.ShouldBe(string.Concat("before", PrintableText.Replacement, "after"));
    }

    // The text is given as UTF-16 code units, because an attribute stores a string as UTF-8, which cannot hold an
    // unpaired surrogate.
    [Theory]
    [InlineData(new[] { 'a', '\uD800', 'b' }, "a\uFFFDb")]
    [InlineData(new[] { 'a', '\uDFFF', 'b' }, "a\uFFFDb")]
    [InlineData(new[] { 'e', 'n', 'd', '\uD83D' }, "end\uFFFD")]
    [InlineData(new[] { '\uDE00', 'g', 'o' }, "\uFFFDgo")]
    [InlineData(new[] { 'a', '\uDE00', '\uD83D', 'b' }, "a\uFFFD\uFFFDb")]
    [InlineData(new[] { '\uD83D', '\uD83D', '\uDE00' }, "\uFFFD\U0001F600")]
    public void OneLine_replaces_each_unpaired_surrogate_with_one_replacement_character(char[] codeUnits, string expected)
    {
        // Arrange
        string text = new(codeUnits);

        // Act
        string printable = PrintableText.OneLine(text);

        // Assert
        printable.ShouldBe(expected);
    }

    [Fact]
    public void OneLine_neutralizes_every_control_format_separator_and_surrogate_code_point_and_keeps_every_other()
    {
        // Act
        IReadOnlyList<string> wronglyPrinted = PrintableTextSamples.WronglyPrinted();

        // Assert
        wronglyPrinted.ShouldBeEmpty("every Cc, Cf, Zl, Zp, and Cs code point and every other default ignorable one except a variation selector must be neutralized, and nothing else");
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
