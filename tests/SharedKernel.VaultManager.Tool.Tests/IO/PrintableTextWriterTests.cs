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
        using var output = new StringWriter(CultureInfo.InvariantCulture);
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
        using var output = new StringWriter(CultureInfo.InvariantCulture);
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
        using var output = new StringWriter(CultureInfo.InvariantCulture);
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
    public void Flush_replaces_a_high_surrogate_that_no_low_surrogate_follows()
    {
        // Arrange
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var writer = new PrintableTextWriter(output);

        // Act
        writer.Write("end\uD83D");
        writer.Flush();

        // Assert
        output.ToString().ShouldBe("end\uFFFD");
    }
}
