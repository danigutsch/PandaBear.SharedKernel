using PandaBear.Testing;

namespace PandaBear.InputNormalization.Tests;

/// <summary>Unit tests for <see cref="UrlSlug" />.</summary>
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.InputNormalizationCapability)]
public sealed class UrlSlugTests
{
    [Theory]
    [InlineData("Viagem à Patagônia", "viagem-a-patagonia")]
    [InlineData("  Café   com_leite--2026 ", "cafe-com-leite-2026")]
    [InlineData("Tour 42", "tour-42")]
    [InlineData("already-canonical", "already-canonical")]
    [InlineData("-_ lead and trail _-", "lead-and-trail")]
    public void TryNormalize_builds_a_lowercase_ascii_slug(string input, string expected)
    {
        // Act
        var normalized = UrlSlug.TryNormalize(input, out var slug);

        // Assert
        normalized.ShouldBeTrue();
        slug.ShouldBe(expected);
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
    public void TryNormalize_rejects_unsafe_or_empty_input(string? input)
    {
        // Act
        var normalized = UrlSlug.TryNormalize(input, out var slug);

        // Assert
        normalized.ShouldBeFalse();
        slug.ShouldBe(string.Empty);
    }

    [Fact]
    public void TryNormalize_rejects_a_slug_longer_than_the_default_limit()
    {
        // Arrange
        var tooLong = new string('a', UrlSlug.DefaultMaxLength + 1);
        var atLimit = new string('a', UrlSlug.DefaultMaxLength);

        // Act
        var rejected = UrlSlug.TryNormalize(tooLong, out _);
        var accepted = UrlSlug.TryNormalize(atLimit, out _);

        // Assert
        rejected.ShouldBeFalse();
        accepted.ShouldBeTrue();
    }

    [Fact]
    public void TryNormalize_honors_a_custom_limit()
    {
        // Act
        var accepted = UrlSlug.TryNormalize("abcd", out var slug, maxLength: 4);
        var rejected = UrlSlug.TryNormalize("abcde", out _, maxLength: 4);

        // Assert
        accepted.ShouldBeTrue();
        slug.ShouldBe("abcd");
        rejected.ShouldBeFalse();
    }

    [Fact]
    public void TryNormalize_rejects_a_non_positive_limit()
    {
        // Act
        var normalize = () => { UrlSlug.TryNormalize("a", out _, maxLength: 0); };

        // Assert
        normalize.ShouldThrow<ArgumentOutOfRangeException>();
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
    public void IsCanonical_accepts_only_already_normalized_slugs(string? value, bool expected)
    {
        // Act
        var canonical = UrlSlug.IsCanonical(value);

        // Assert
        canonical.ShouldBe(expected);
    }

    [Fact]
    public void IsCanonical_honors_a_custom_limit()
    {
        // Act
        var canonical = UrlSlug.IsCanonical("abcde", maxLength: 4);

        // Assert
        canonical.ShouldBeFalse();
    }
}
