using PandaBear.Results;

namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Tests the canonical URL slug value object.</summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "ValueObjects")]
public sealed class SlugTests
{
    [Theory]
    [InlineData("Viagem à Patagônia", "viagem-a-patagonia")]
    [InlineData("  Café   com_leite--2026 ", "cafe-com-leite-2026")]
    [InlineData("Tour 42", "tour-42")]
    [InlineData("already-canonical", "already-canonical")]
    [InlineData("-_ lead and trail _-", "lead-and-trail")]
    public void Free_text_becomes_a_lowercase_ascii_slug(string input, string expected)
    {
        // Act
        var slug = Slug.Create(input).ShouldBeSuccess();

        // Assert
        slug.Value.ShouldBe(expected);
        slug.ToString().ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a?b")]
    [InlineData("a#b")]
    [InlineData("a\nb")]
    [InlineData("a\0b")]
    [InlineData("a.b")]
    [InlineData("tour & co")]
    [InlineData("東京")]
    [InlineData("tour \uFF14\uFF12")]
    [InlineData("straße")]
    public void Unsafe_or_empty_input_is_rejected_without_echoing_it(string? input)
    {
        // Act
        var result = Slug.Create(input, field: "slug", label: "Slug");

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        var message = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["slug"].ShouldHaveSingleItem();
        var echoed = !string.IsNullOrWhiteSpace(input) && message.Contains(input, StringComparison.Ordinal);
        echoed.ShouldBeFalse();
    }

    [Fact]
    public void A_lone_surrogate_is_rejected_instead_of_throwing()
    {
        // Arrange
        // Lone surrogates are built in code: attribute arguments are stored as UTF-8 and would arrive as U+FFFD.
        string[] inputs = ["a\uD800", "\uDC00a", "tour \uD83D"];

        // Act
        var statuses = inputs.Select(static input => Slug.Create(input).Status).ToList();
        var canonical = inputs.Select(static input => Slug.IsCanonical(input)).ToList();

        // Assert
        statuses.ShouldBe([ResultStatus.Invalid, ResultStatus.Invalid, ResultStatus.Invalid]);
        canonical.ShouldBe([false, false, false]);
    }

    [Fact]
    public void A_slug_longer_than_the_default_limit_is_rejected()
    {
        // Act
        var tooLong = Slug.Create(new string('a', Slug.DefaultMaxLength + 1));
        var atLimit = Slug.Create(new string('a', Slug.DefaultMaxLength));

        // Assert
        tooLong.Status.ShouldBe(ResultStatus.Invalid);
        atLimit.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void A_custom_limit_applies_to_the_normalized_slug()
    {
        // Act
        var accepted = Slug.Create("a b c", maxLength: 5);
        var rejected = Slug.Create("a b c d", maxLength: 5);

        // Assert
        accepted.ShouldBeSuccess().Value.ShouldBe("a-b-c");
        rejected.Status.ShouldBe(ResultStatus.Invalid);
    }

    [Fact]
    public void A_non_positive_limit_is_a_caller_error()
    {
        // Act
        var create = () => { Slug.Create("a", maxLength: 0); };

        // Assert
        create.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Slugs_from_different_spellings_of_the_same_text_are_equal()
    {
        // Act
        var first = Slug.Create("Café com Leite").ShouldBeSuccess();
        var second = Slug.Create("cafe-com-leite").ShouldBeSuccess();

        // Assert
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Theory]
    [InlineData("tour-42", true)]
    [InlineData("a", true)]
    [InlineData("Tour-42", false)]
    [InlineData("tour--42", false)]
    [InlineData("-tour", false)]
    [InlineData("tour-", false)]
    [InlineData("tour 42", false)]
    [InlineData("café", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("tour-\uFF14\uFF12", false)]
    [InlineData("straße", false)]
    public void Only_an_already_normalized_value_is_canonical(string? value, bool expected)
    {
        // Act
        var canonical = Slug.IsCanonical(value);

        // Assert
        canonical.ShouldBe(expected);
    }

    [Fact]
    public void Canonical_checks_honor_a_custom_limit()
    {
        // Act
        var canonical = Slug.IsCanonical("abcde", maxLength: 4);

        // Assert
        canonical.ShouldBeFalse();
    }

    [Fact]
    public void Restore_rebuilds_a_stored_slug()
    {
        // Act
        var slug = Slug.Restore("tour-42");

        // Assert
        slug.ShouldBe(Slug.Create("Tour 42").ShouldBeSuccess());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Restore_rejects_a_blank_value(string? value)
    {
        // Act
        var restore = () => { Slug.Restore(value!); };

        // Assert
        restore.ShouldThrow<ArgumentException>();
    }
}
