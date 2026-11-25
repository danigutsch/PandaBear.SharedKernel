namespace SharedKernel.InputNormalization.Tests;

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
    [InlineData("")]
    [InlineData("Bank | Visa ~ {Login} 42!")]
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

    [Theory]
    [InlineData("\U0001F469\u200D\U0001F4BB")]
    [InlineData("Bank \U0001F468\u200D\U0001F469\u200D\U0001F467\u200D\U0001F466 team")]
    [InlineData("\U0001F3F3\uFE0F\u200D\U0001F308")]
    [InlineData("\u2764\uFE0F\u200D\U0001F525")]
    [InlineData("\U0001F9D1\U0001F3FD\u200D\U0001F4BB")]
    [InlineData("\U0001F408\u200D\u2B1B")]
    public void OneLine_keeps_a_zero_width_joiner_between_two_emoji_of_an_emoji_zwj_sequence(string text)
    {
        // Act
        string printable = PrintableText.OneLine(text);

        // Assert
        printable.ShouldBeSameAs(text);
    }

    [Theory]
    [InlineData("\U0001F3F4\U000E0067\U000E0062\U000E0065\U000E006E\U000E0067\U000E007F")]
    [InlineData("Glasgow \U0001F3F4\U000E0067\U000E0062\U000E0073\U000E0063\U000E0074\U000E007F Bank")]
    [InlineData("\U0001F3F4\U000E0067\U000E0062\U000E0077\U000E006C\U000E0073\U000E007F\U0001F3F4\U000E0075\U000E0073\U000E0074\U000E0078\U000E007F")]
    public void OneLine_keeps_the_tag_characters_of_a_well_formed_emoji_tag_sequence(string text)
    {
        // Act
        string printable = PrintableText.OneLine(text);

        // Assert
        printable.ShouldBeSameAs(text);
    }

    [Theory]
    [InlineData("\u0645\u06CC\u200C\u062E\u0648\u0627\u0647\u0645")]
    [InlineData("\u0628\u0647\u200C\u0631\u0648\u0632")]
    [InlineData("\u0646\u0627\u0645\u0647\u200C\u0627\u06CC")]
    [InlineData("\u0628\u064E\u200C\u0631")]
    [InlineData("\u0D26\u0D43\u0D15\u0D4D\u200C\u0D38\u0D3E\u0D15\u0D4D\u0D37\u0D3F")]
    [InlineData("\u0915\u094D\u200C\u0937")]
    [InlineData("\u0DC1\u0DCA\u200D\u0DBB\u0DD3 \u0DBD\u0D82\u0D9A\u0DCF")]
    [InlineData("\u0915\u094D\u200D\u0937")]
    public void OneLine_keeps_a_joiner_or_non_joiner_in_the_joining_contexts_of_uts39(string text)
    {
        // Act
        string printable = PrintableText.OneLine(text);

        // Assert
        printable.ShouldBeSameAs(text);
    }

    [Theory]
    [InlineData("\u200D\U0001F469", "\uFFFD\U0001F469")]
    [InlineData("\U0001F469\u200D", "\U0001F469\uFFFD")]
    [InlineData("\U0001F469 \u200D\U0001F4BB", "\U0001F469 \uFFFD\U0001F4BB")]
    [InlineData("\U0001F469\u200D \U0001F4BB", "\U0001F469\uFFFD \U0001F4BB")]
    [InlineData("\U0001F469\u200D\u200D\U0001F4BB", "\U0001F469\uFFFD\uFFFD\U0001F4BB")]
    [InlineData("\U0001F469\u200C\U0001F4BB", "\U0001F469\uFFFD\U0001F4BB")]
    [InlineData("Git\u200DHub", "Git\uFFFDHub")]
    [InlineData("f\u200Dinland", "f\uFFFDinland")]
    [InlineData("V\u200Cersion", "V\uFFFDersion")]
    [InlineData("\U0001F469\u200Da", "\U0001F469\uFFFDa")]
    [InlineData("a\u200D\U0001F4BB", "a\uFFFD\U0001F4BB")]
    [InlineData("\U0001F469\u200D\u202E\U0001F4BB", "\U0001F469\uFFFD\uFFFD\U0001F4BB")]
    [InlineData("\U0001F469\u200D\r\U0001F4BB", "\U0001F469\uFFFD \U0001F4BB")]
    public void OneLine_replaces_a_zero_width_joiner_or_non_joiner_outside_an_emoji_zwj_sequence(string text, string expected)
    {
        // Act
        string printable = PrintableText.OneLine(text);

        // Assert
        printable.ShouldBe(expected);
    }

    [Theory]
    [InlineData("\u200C\u0628\u0631", "\uFFFD\u0628\u0631")]
    [InlineData("\u0628\u200C", "\u0628\uFFFD")]
    [InlineData("\u0628\u200C\u200C\u0631", "\u0628\uFFFD\uFFFD\u0631")]
    [InlineData("\u0628 \u200C\u0631", "\u0628 \uFFFD\u0631")]
    [InlineData("\u0628\u0647\u200C\u0631\u0648\u200C\u0632", "\u0628\u0647\u200C\u0631\u0648\uFFFD\u0632")]
    [InlineData("\u0627\u200C\u0628", "\u0627\uFFFD\u0628")]
    [InlineData("\u0628\u200C\u0621", "\u0628\uFFFD\u0621")]
    [InlineData("\u0628\u200D\u0631", "\u0628\uFFFD\u0631")]
    [InlineData("\u0915\u200D\u0937", "\u0915\uFFFD\u0937")]
    [InlineData("\u0915\u200C\u0937", "\u0915\uFFFD\u0937")]
    [InlineData("\u0915\u094D\u200D", "\u0915\u094D\uFFFD")]
    [InlineData("\u0915\u094D\u200C", "\u0915\u094D\uFFFD")]
    [InlineData("\u0915\u094D\u200D\u093F", "\u0915\u094D\uFFFD\u093F")]
    [InlineData("\u0915\u094D\u200D \u0937", "\u0915\u094D\uFFFD \u0937")]
    [InlineData("\u094D\u200D\u0937", "\u094D\uFFFD\u0937")]
    [InlineData("\u0915\u094D\u200D\u200D\u0937", "\u0915\u094D\uFFFD\uFFFD\u0937")]
    public void OneLine_replaces_a_zero_width_joiner_or_non_joiner_outside_the_joining_contexts_of_uts39(string text, string expected)
    {
        // Act
        string printable = PrintableText.OneLine(text);

        // Assert
        printable.ShouldBe(expected);
    }

    [Theory]
    [InlineData("\U0001F3F4\U000E0067\U000E0062\U000E0065\U000E006E\U000E0067", "\U0001F3F4\uFFFD\uFFFD\uFFFD\uFFFD\uFFFD")]
    [InlineData("\U0001F3F4\U000E0067\U000E0062 x", "\U0001F3F4\uFFFD\uFFFD x")]
    [InlineData("x\U000E0067\U000E0062\U000E0065\U000E006E\U000E0067\U000E007F", "x\uFFFD\uFFFD\uFFFD\uFFFD\uFFFD\uFFFD")]
    [InlineData("\U0001F600\U000E0067\U000E0062\U000E0065\U000E006E\U000E0067\U000E007F", "\U0001F600\uFFFD\uFFFD\uFFFD\uFFFD\uFFFD\uFFFD")]
    [InlineData("\U0001F3F4\U000E007F", "\U0001F3F4\uFFFD")]
    [InlineData("\U0001F3F4 \U000E0067\U000E0062\U000E0065\U000E006E\U000E0067\U000E007F", "\U0001F3F4 \uFFFD\uFFFD\uFFFD\uFFFD\uFFFD\uFFFD")]
    [InlineData("\U0001F3F4\U000E0067\U000E0062\U000E0065\U000E006E\U000E0067\U000E007F\U000E007F", "\U0001F3F4\U000E0067\U000E0062\U000E0065\U000E006E\U000E0067\U000E007F\uFFFD")]
    [InlineData("\U0001F3F4\U000E0047\U000E0042\U000E0045\U000E004E\U000E0047\U000E007F", "\U0001F3F4\uFFFD\uFFFD\uFFFD\uFFFD\uFFFD\uFFFD")]
    [InlineData("\U0001F3F4\U000E0067\U000E0062\U000E0020\U000E006E\U000E007F", "\U0001F3F4\uFFFD\uFFFD\uFFFD\uFFFD\uFFFD")]
    [InlineData("\U0001F3F4\U000E0001\U000E0067\U000E0062\U000E0065\U000E007F", "\U0001F3F4\uFFFD\uFFFD\uFFFD\uFFFD\uFFFD")]
    [InlineData("\U0001F3F4\U000E0069\U000E0067\U000E006E\U000E006F\U000E0072\U000E0065\U000E0061\U000E006C\U000E007F", "\U0001F3F4\uFFFD\uFFFD\uFFFD\uFFFD\uFFFD\uFFFD\uFFFD\uFFFD\uFFFD")]
    public void OneLine_replaces_each_tag_character_outside_a_well_formed_emoji_tag_sequence(string text, string expected)
    {
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
