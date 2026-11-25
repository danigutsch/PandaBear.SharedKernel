using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks.Tests;

/// <summary>Tests the shared validation of plain text a person entered.</summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "ValueObjects")]
public sealed class SanitizedTextTests
{
    private const string Field = "title";
    private const string LoneSurrogate = "a\uD800b";

    [Fact]
    public void Required_sanitizes_the_text()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        var text = SanitizedText.Required(errors, "  Rua \t A  ", Field, "Title", 10);

        // Assert
        text.ShouldBe("Rua A");
        errors.HasErrors.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null, "Title is required.")]
    [InlineData(" \u0001 ", "Title is required.")]
    [InlineData("abcdefghijk", "Title cannot exceed 10 characters.")]
    public void Required_returns_nothing_for_refused_text(string? value, string message)
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        var text = SanitizedText.Required(errors, value, Field, "Title", 10);

        // Assert
        text.ShouldBeNull();
        FieldMessages.Of(errors, Field).ShouldBe([message]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Optional_blank_text_is_null_without_an_error(string? value)
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        var text = SanitizedText.Optional(errors, value, Field, "Title", 10);

        // Assert
        text.ShouldBeNull();
        errors.HasErrors.ShouldBeFalse();
    }

    [Theory]
    [InlineData("abcdefghijk", "Title cannot exceed 10 characters.")]
    public void Optional_returns_nothing_for_refused_text(string value, string message)
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        var text = SanitizedText.Optional(errors, value, Field, "Title", 10);

        // Assert
        text.ShouldBeNull();
        FieldMessages.Of(errors, Field).ShouldBe([message]);
    }

    [Fact]
    public void Paragraphs_split_on_blank_lines()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        var paragraphs = SanitizedText.Paragraphs(errors, "One\ntwo\n\n  Three ", Field, "Text", 2, 10);

        // Assert
        paragraphs.ShouldBe(["One two", "Three"]);
        errors.HasErrors.ShouldBeFalse();
    }

    [Theory]
    [InlineData("a\n\nb\n\nc", "Text cannot have more than 2 paragraphs.")]
    [InlineData("abcdefghijk", "Text: each paragraph must be at most 10 characters.")]
    public void Paragraphs_return_nothing_for_refused_text(string value, string message)
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        var paragraphs = SanitizedText.Paragraphs(errors, value, Field, "Text", 2, 10);

        // Assert
        paragraphs.ShouldBeEmpty();
        FieldMessages.Of(errors, Field).ShouldBe([message]);
    }

    [Fact]
    public void Malformed_text_is_refused_and_returns_nothing()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        var required = SanitizedText.Required(errors, LoneSurrogate, Field, "Title", 10);
        var optional = SanitizedText.Optional(errors, LoneSurrogate, Field, "Subtitle", 10);
        var paragraphs = SanitizedText.Paragraphs(errors, LoneSurrogate, Field, "Text", 2, 10);

        // Assert
        required.ShouldBeNull();
        optional.ShouldBeNull();
        paragraphs.ShouldBeEmpty();
        FieldMessages.Of(errors, Field).ShouldBe(["Title is not valid text.", "Subtitle is not valid text.", "Text is not valid text."]);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(" \u0001 ", false)]
    [InlineData("abc", true)]
    [InlineData("abcdefghijk", true)]
    public void Optional_tells_whether_text_was_entered_even_when_it_is_refused(string? value, bool expected)
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        _ = SanitizedText.Optional(errors, value, Field, "Title", 10, out var entered);

        // Assert
        entered.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("\n \n", false)]
    [InlineData("One", true)]
    [InlineData("a\n\nb\n\nc", true)]
    [InlineData("abcdefghijk", true)]
    public void Paragraphs_tell_whether_text_was_entered_even_when_it_is_refused(string? value, bool expected)
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        _ = SanitizedText.Paragraphs(errors, value, Field, "Text", 2, 10, out var entered);

        // Assert
        entered.ShouldBe(expected);
    }

    [Fact]
    public void Malformed_text_counts_as_entered()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        var optional = SanitizedText.Optional(errors, LoneSurrogate, Field, "Subtitle", 10, out var optionalEntered);
        var paragraphs = SanitizedText.Paragraphs(errors, LoneSurrogate, Field, "Text", 2, 10, out var paragraphsEntered);

        // Assert
        optional.ShouldBeNull();
        paragraphs.ShouldBeEmpty();
        optionalEntered.ShouldBeTrue();
        paragraphsEntered.ShouldBeTrue();
    }

    [Fact]
    public void Lines_sanitize_every_item()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        var lines = SanitizedText.Lines(errors, [" One ", "Two"], Field, "Highlights", 2, 10);

        // Assert
        lines.ShouldBe(["One", "Two"]);
        errors.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public void Lines_of_none_are_empty_without_an_error()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        var lines = SanitizedText.Lines(errors, null, Field, "Highlights", 2, 10);

        // Assert
        lines.ShouldBeEmpty();
        errors.HasErrors.ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(RefusedLineCases.All), MemberType = typeof(RefusedLineCases))]
    public void Lines_return_nothing_for_a_refused_list(string?[] values, string message)
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        var lines = SanitizedText.Lines(errors, values, Field, "Highlights", 2, 10);

        // Assert
        lines.ShouldBeEmpty();
        FieldMessages.Of(errors, Field).ShouldBe([message]);
    }
}
