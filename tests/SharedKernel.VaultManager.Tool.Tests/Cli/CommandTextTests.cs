using PandaBear.VaultManager.Tool.Cli;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

public sealed class CommandTextTests
{
    [Theory]
    [InlineData(0x00)]
    [InlineData(0x07)]
    [InlineData(0x09)]
    [InlineData(0x0A)]
    [InlineData(0x0D)]
    [InlineData(0x1B)]
    [InlineData(0x1F)]
    [InlineData(0x7F)]
    [InlineData(0x80)]
    [InlineData(0x85)]
    [InlineData(0x9B)]
    [InlineData(0x9F)]
    public void One_line_shows_every_control_character_as_a_space(int controlCharacter)
    {
        string text = string.Concat("before", (char)controlCharacter, "after");

        string shown = CommandText.OneLine(text);

        shown.ShouldBe("before after");
    }

    [Fact]
    public void One_line_keeps_printable_text_as_it_is()
    {
        string text = string.Concat("Bob's Café Bank ", (char)0xA0, "€ 😀");

        string shown = CommandText.OneLine(text);

        shown.ShouldBe(text);
    }
}
