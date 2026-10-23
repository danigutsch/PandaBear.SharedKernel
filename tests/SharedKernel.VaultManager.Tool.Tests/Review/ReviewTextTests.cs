using PandaBear.VaultManager.Tool.Review;

namespace PandaBear.VaultManager.Tool.Tests.Review;

public sealed class ReviewTextTests
{
    [Theory]
    [InlineData("Bank\u001b[2J", "Bank\uFFFD[2J")]
    [InlineData("Bank\u009b31m", "Bank\uFFFD31m")]
    [InlineData("invoice\u202Etxt.exe", "invoice\uFFFDtxt.exe")]
    [InlineData("\u2066Bank\u2069", "\uFFFDBank\uFFFD")]
    [InlineData("a\tb", "a b")]
    public void One_line_replaces_terminal_controls_and_bidirectional_overrides(string text, string expected)
    {
        string shown = ReviewText.OneLine(text);

        shown.ShouldBe(expected);
    }

    [Theory]
    [InlineData("two\nlines")]
    [InlineData("two\r\nlines")]
    [InlineData("two\rlines")]
    [InlineData("two\u2028lines")]
    [InlineData("two\u2029lines")]
    public void One_line_marks_each_line_break(string text)
    {
        string shown = ReviewText.OneLine(text);

        shown.ShouldBe("two" + ReviewText.LineBreak + "lines");
    }
}
