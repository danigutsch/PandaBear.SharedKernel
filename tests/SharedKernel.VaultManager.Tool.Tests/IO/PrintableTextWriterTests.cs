using System.Globalization;
using PandaBear.Testing;

namespace PandaBear.VaultManager.Tool.Tests.IO;

/// <summary>Tests the text writer that replaces unprintable characters before they reach the wrapped writer.</summary>
public sealed class PrintableTextWriterTests
{
    [Theory]
    [InlineData("Bank\u001B[2J\u009B31m\u202Etxt", "Bank\uFFFD[2J\uFFFD31m\uFFFDtxt")]
    [InlineData("one\ntwo\r\nthree", "one\ntwo\r\nthree")]
    [InlineData("back\rto the start\tand a tab", "back to the start and a tab")]
    public void Writes_replace_control_and_bidirectional_characters_and_keep_line_breaks(string text, string expected)
    {
        // Arrange
        char[] characters = [.. text];
        using var output = new InvariantStringWriter();
        using var writer = new PrintableTextWriter(output);

        // Act
        writer.Write(text);
        writer.Write(characters);
        foreach (char character in "\u001B\n")
        {
            writer.Write(character);
        }

        // Assert
        output.ToString().ShouldBe(expected + expected + "\uFFFD\n");
    }

    [Fact]
    public void Write_line_ends_with_the_line_break_of_the_other_writer()
    {
        // Arrange
        using var output = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\r\n" };
        using var writer = new PrintableTextWriter(output);

        // Act
        writer.WriteLine("x\u001By");
        writer.WriteLine();

        // Assert
        output.ToString().ShouldBe("x\uFFFDy\r\n\r\n");
    }

    [Theory]
    [InlineData("Git\u200BHub\u200E\uFEFF\u00AD", "Git\uFFFDHub\uFFFD\uFFFD\uFFFD")]
    [InlineData("tag\U000E0041\nnext\r\nend", "tag\uFFFD\nnext\r\nend")]
    public void Writes_replace_invisible_format_characters_and_keep_line_breaks(string text, string expected)
    {
        // Arrange
        using var output = new InvariantStringWriter();
        using var writer = new PrintableTextWriter(output);

        // Act
        writer.Write(text);
        writer.Flush();

        // Assert
        output.ToString().ShouldBe(expected);
    }

    [Fact]
    public void Write_replaces_an_unpaired_surrogate_and_keeps_a_surrogate_pair_and_line_break()
    {
        // Arrange
        using var output = new InvariantStringWriter();
        using var writer = new PrintableTextWriter(output);

        // Act
        writer.Write("lone\uD800\nkept \U0001F600");
        writer.Flush();

        // Assert
        output.ToString().ShouldBe("lone\uFFFD\nkept \U0001F600");
    }

    [Fact]
    public void Write_keeps_a_surrogate_pair_written_one_character_at_a_time()
    {
        // Arrange
        using var output = new InvariantStringWriter();
        using var writer = new PrintableTextWriter(output);

        // Act
        foreach (char character in "a\U0001F600b")
        {
            writer.Write(character);
        }

        writer.Write("\uD83D");
        writer.Write("\uDC4D!");
        writer.Flush();

        // Assert
        output.ToString().ShouldBe("a\U0001F600b\U0001F44D!");
    }

    [Fact]
    public void Flush_keeps_a_high_surrogate_waiting_for_the_low_surrogate_of_the_next_write()
    {
        // Arrange
        using var output = new InvariantStringWriter();
        using var writer = new PrintableTextWriter(output);

        // Act
        writer.Write("a\uD83D");
        writer.Flush();
        string flushed = output.ToString();
        writer.Write("\uDE00b");
        writer.Flush();

        // Assert
        flushed.ShouldBe("a");
        output.ToString().ShouldBe("a\U0001F600b");
    }

    [Fact]
    public void Dispose_replaces_a_high_surrogate_that_no_low_surrogate_follows()
    {
        // Arrange
        using var output = new InvariantStringWriter();
        var writer = new PrintableTextWriter(output);
        writer.Write("end\uD83D");
        writer.Flush();

        // Act
        writer.Dispose();

        // Assert
        output.ToString().ShouldBe("end\uFFFD");
    }

    [Theory]
    [InlineData("\U0001F469\u200D\U0001F4BB\n\U0001F3F4\U000E0067\U000E0062\U000E0065\U000E006E\U000E0067\U000E007F\r\n\u0645\u06CC\u200C\u062E\u0648\u0627\u0647\u0645", "\U0001F469\u200D\U0001F4BB\n\U0001F3F4\U000E0067\U000E0062\U000E0065\U000E006E\U000E0067\U000E007F\r\n\u0645\u06CC\u200C\u062E\u0648\u0627\u0647\u0645")]
    [InlineData("\U0001F469\u200D\n\U0001F4BB \u200C\r\n", "\U0001F469\uFFFD\n\U0001F4BB \uFFFD\r\n")]
    [InlineData("plain ASCII\r\nlines\n", "plain ASCII\r\nlines\n")]
    public void Write_keeps_joiners_and_tag_sequences_where_one_line_keeps_them_and_keeps_line_breaks(string text, string expected)
    {
        // Arrange
        using var output = new InvariantStringWriter();
        using var writer = new PrintableTextWriter(output);

        // Act
        writer.Write(text);
        writer.Flush();

        // Assert
        output.ToString().ShouldBe(expected);
    }
}
