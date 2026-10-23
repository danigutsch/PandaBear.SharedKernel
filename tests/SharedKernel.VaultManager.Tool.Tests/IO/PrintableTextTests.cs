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
        string printable = PrintableText.OneLine(text);

        printable.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Bank\u2028# Injected heading", "Bank # Injected heading")]
    [InlineData("Bank\u2029# Injected heading", "Bank # Injected heading")]
    public void OneLine_turns_line_and_paragraph_separators_into_spaces(string text, string expected)
    {
        string printable = PrintableText.OneLine(text);

        printable.ShouldBe(expected);
    }

    [Theory]
    [InlineData("invoice\u202Etxt.exe", "invoice\uFFFDtxt.exe")]
    [InlineData("\u2066Bank\u2069", "\uFFFDBank\uFFFD")]
    [InlineData("a\u202Ab\u202Bc\u202Cd\u202De\u2067f\u2068g", "a\uFFFDb\uFFFDc\uFFFDd\uFFFDe\uFFFDf\uFFFDg")]
    public void OneLine_replaces_bidirectional_embeddings_overrides_and_isolates(string text, string expected)
    {
        string printable = PrintableText.OneLine(text);

        printable.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Bank | Visa ü €")]
    [InlineData("\u05D1\u05E0\u05E7 \u200FBank\u200E")]
    public void OneLine_returns_text_without_control_characters_as_it_is(string text)
    {
        string printable = PrintableText.OneLine(text);

        printable.ShouldBeSameAs(text);
    }

    [Fact]
    public void TableCell_also_escapes_the_cell_separator()
    {
        string cell = PrintableText.TableCell("a|b\u001b\u2028c");

        cell.ShouldBe("a\\|b\uFFFD c");
    }
}
