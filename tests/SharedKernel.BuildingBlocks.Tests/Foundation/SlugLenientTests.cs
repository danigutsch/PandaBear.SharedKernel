using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks.Tests;

/// <summary>Tests the lenient slug factory, which turns any text into a slug instead of rejecting unsafe characters.</summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "ValueObjects")]
public sealed class SlugLenientTests
{
    [Theory]
    [InlineData("ÁLPES/2027 ..\\x", "alpes-2027-x")]
    [InlineData("  CN__ 01  ", "cn-01")]
    [InlineData("CN-APR-27", "cn-apr-27")]
    [InlineData("Viagem à Patagônia", "viagem-a-patagonia")]
    [InlineData("tour & co", "tour-co")]
    [InlineData("a.b/c\\d?e#f", "a-b-c-d-e-f")]
    [InlineData("tour\n42\0", "tour-42")]
    [InlineData("東京 2027", "2027")]
    [InlineData("straße", "stra-e")]
    public void Any_text_becomes_a_canonical_slug(string input, string expected)
    {
        // Act
        var slug = Slug.CreateLenient(input).ShouldBeSuccess();

        // Assert
        slug.Value.ShouldBe(expected);
        Slug.IsCanonical(slug.Value).ShouldBeTrue();
    }

    [Theory]
    [InlineData("Viagem à Patagônia")]
    [InlineData("  Café   com_leite--2026 ")]
    [InlineData("-_ lead and trail _-")]
    public void Text_that_create_accepts_gives_the_same_slug(string input)
    {
        // Act
        var lenient = Slug.CreateLenient(input).ShouldBeSuccess();

        // Assert
        lenient.ShouldBe(Slug.Create(input).ShouldBeSuccess());
    }

    [Fact]
    public void Surrogates_separate_words_instead_of_throwing()
    {
        // Arrange
        // Lone surrogates are built in code: attribute arguments are stored as UTF-8 and would arrive as U+FFFD.
        string[] inputs = ["a\uD800b", "\uDC00tour", "tour \uD83D\uDE00 42"];

        // Act
        var slugs = inputs.Select(static input => Slug.CreateLenient(input).ShouldBeSuccess().Value).ToList();
        var loneOnly = Slug.CreateLenient("\uD83D");

        // Assert
        slugs.ShouldBe(["a-b", "tour", "tour-42"]);
        loneOnly.Status.ShouldBe(ResultStatus.Invalid);
    }

    [Theory]
    [InlineData("中国")]
    [InlineData("---")]
    [InlineData("./\\")]
    public void Text_without_an_ascii_letter_or_digit_fails_without_echoing_it(string input)
    {
        // Act
        var result = Slug.CreateLenient(input, field: "tripCode", label: "Trip code");

        // Assert
        var error = result.ErrorDetails.ShouldNotBeNull();
        error.Detail.ShouldBe("Trip code must contain a letter or a digit.");
        error.ValidationErrors.ShouldNotBeNull()["tripCode"].ShouldBe(["Trip code must contain a letter or a digit."]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \t ")]
    public void Blank_text_is_required(string? input)
    {
        // Act
        var result = Slug.CreateLenient(input);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["slug"].ShouldBe(["Slug is required."]);
    }

    [Theory]
    [InlineData(7, "abc-def")]
    [InlineData(8, "abc-def")]
    [InlineData(9, "abc-def-g")]
    [InlineData(1, "a")]
    public void A_longer_slug_is_cut_at_the_limit_without_a_trailing_hyphen(int maxLength, string expected)
    {
        // Act
        var slug = Slug.CreateLenient("ABC def/ghi", maxLength).ShouldBeSuccess();

        // Assert
        slug.Value.ShouldBe(expected);
    }

    [Fact]
    public void A_slug_within_the_default_limit_is_kept_whole()
    {
        // Arrange
        var input = new string('a', Slug.DefaultMaxLength);

        // Act
        var slug = Slug.CreateLenient(input + "!").ShouldBeSuccess();

        // Assert
        slug.Value.ShouldBe(input);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_non_positive_limit_is_a_caller_error(int maxLength)
    {
        // Arrange
        var create = () => { Slug.CreateLenient("tour", maxLength); };

        // Act & Assert
        create.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
