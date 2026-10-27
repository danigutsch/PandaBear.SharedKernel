using System.Globalization;
using PandaBear.VaultManager.Tool.IO;

namespace PandaBear.VaultManager.Tool.Tests.IO;

/// <summary>Tests the text writer that replaces unprintable characters before they reach the wrapped writer.</summary>
public sealed class PrintableTextWriterTests
{
    /// <summary>Writing a string, a character array, and single characters replaces escape, C1, and override characters with U+FFFD, turns carriage return and tab into spaces, and keeps line feeds.</summary>
    /// <param name="text">The text to write.</param>
    /// <param name="expected">The expected output of one write of the text.</param>
    [Theory]
    [InlineData("Bank\u001B[2J\u009B31m\u202Etxt", "Bank\uFFFD[2J\uFFFD31m\uFFFDtxt")]
    [InlineData("one\ntwo\r\nthree", "one\ntwo\r\nthree")]
    [InlineData("back\rto the start\tand a tab", "back to the start and a tab")]
    public void Writes_replace_control_and_bidirectional_characters_and_keep_line_breaks(string text, string expected)
    {
        // Arrange
        char[] characters = [.. text];
        using var output = new StringWriter(CultureInfo.InvariantCulture);
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

    /// <summary>WriteLine ends each line with the new-line string of the wrapped writer, here CRLF, and still replaces the escape character in the text.</summary>
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
}
