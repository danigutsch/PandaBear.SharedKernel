namespace PandaBear.InputNormalization.Tests;

/// <summary>Tests splitting plain text into sanitized paragraphs on blank lines.</summary>
public sealed class StringSanitizerParagraphTests
{
    [Fact]
    public void Text_is_split_on_blank_lines_and_line_breaks_inside_a_paragraph_become_spaces()
    {
        // Arrange
        const string text = "  First line\r\nstill first  \n\n\n Second\t paragraph \r\n   \r\nThird";

        // Act
        var valid = StringSanitizer.TrySanitizeParagraphs(text, out var paragraphs);

        // Assert
        valid.ShouldBeTrue();
        paragraphs.ShouldBe(["First line still first", "Second paragraph", "Third"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \n \r\n\t")]
    public void Missing_or_blank_text_has_no_paragraphs(string? text)
    {
        // Act
        var valid = StringSanitizer.TrySanitizeParagraphs(text, out var paragraphs);

        // Assert
        valid.ShouldBeTrue();
        paragraphs.ShouldBeEmpty();
    }

    [Fact]
    public void A_lone_carriage_return_separates_lines_like_a_line_feed()
    {
        // Act
        var valid = StringSanitizer.TrySanitizeParagraphs("One\rTwo\r\rThree", out var paragraphs);

        // Assert
        valid.ShouldBeTrue();
        paragraphs.ShouldBe(["One Two", "Three"]);
    }

    [Theory]
    [InlineData("\u0085")]
    [InlineData("\u2028")]
    [InlineData("\u2029")]
    [InlineData("\u000C")]
    public void Every_unicode_line_terminator_ends_a_line(string terminator)
    {
        // Arrange
        var text = "One" + terminator + "two" + terminator + terminator + "Three";

        // Act
        var valid = StringSanitizer.TrySanitizeParagraphs(text, out var paragraphs);

        // Assert
        valid.ShouldBeTrue();
        paragraphs.ShouldBe(["One two", "Three"]);
    }

    [Fact]
    public void Joining_the_paragraphs_with_a_blank_line_gives_the_same_paragraphs()
    {
        // Arrange
        StringSanitizer.TrySanitizeParagraphs("A\nb\n\nC", out var first);

        // Act
        StringSanitizer.TrySanitizeParagraphs(string.Join("\n\n", first), out var second);

        // Assert
        second.ShouldBe(first);
    }

    [Fact]
    public void Malformed_text_is_refused_without_an_exception()
    {
        // Act
        var valid = StringSanitizer.TrySanitizeParagraphs("Fine\n\nBroken \uD800 text", out var paragraphs);

        // Assert
        valid.ShouldBeFalse();
        paragraphs.ShouldBeEmpty();
    }
}
