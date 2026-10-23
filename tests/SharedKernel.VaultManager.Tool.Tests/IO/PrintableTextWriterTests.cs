using System.Globalization;
using PandaBear.VaultManager.Tool.IO;

namespace PandaBear.VaultManager.Tool.Tests.IO;

public sealed class PrintableTextWriterTests
{
    [Theory]
    [InlineData("Bank\u001B[2J\u009B31m\u202Etxt", "Bank\uFFFD[2J\uFFFD31m\uFFFDtxt")]
    [InlineData("one\ntwo\r\nthree", "one\ntwo\r\nthree")]
    [InlineData("back\rto the start\tand a tab", "back to the start and a tab")]
    public void Writes_replace_control_and_bidirectional_characters_and_keep_line_breaks(string text, string expected)
    {
        char[] characters = [.. text];
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var writer = new PrintableTextWriter(output);

        writer.Write(text);
        writer.Write(characters);
        foreach (char character in "\u001B\n")
        {
            writer.Write(character);
        }

        output.ToString().ShouldBe(expected + expected + "\uFFFD\n");
    }

    [Fact]
    public void Write_line_ends_with_the_line_break_of_the_other_writer()
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\r\n" };
        using var writer = new PrintableTextWriter(output);

        writer.WriteLine("x\u001By");
        writer.WriteLine();

        output.ToString().ShouldBe("x\uFFFDy\r\n\r\n");
    }
}
