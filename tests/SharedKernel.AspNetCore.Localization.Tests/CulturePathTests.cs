using Microsoft.AspNetCore.Http;
using static PandaBear.AspNetCore.Localization.Tests.LocalizationTestCultures;

namespace PandaBear.AspNetCore.Localization.Tests;

/// <summary>Tests <see cref="CulturePath" />, the helper that reads, prepends, and swaps the culture path segment.</summary>
public sealed class CulturePathTests
{
    [Theory]
    [InlineData("/pt/tours/42", "pt", "/tours/42")]
    [InlineData("/PT/tours", "pt", "/tours")]
    [InlineData("/en", "en", "")]
    [InlineData("/en/", "en", "/")]
    public void TryParse_reads_a_supported_first_segment_and_returns_the_rest(string path, string culture, string remainder)
    {
        // Act
        var parsed = CulturePath.TryParse(new PathString(path), Supported, out var parsedCulture, out var parsedRemainder);

        // Assert
        parsed.ShouldBeTrue();
        parsedCulture.ShouldNotBeNull().Value.ShouldBe(culture);
        parsedRemainder.ToString().ShouldBe(remainder);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("/fr/tours")]
    [InlineData("/pt-BR/tours")]
    [InlineData("/ptx/tours")]
    [InlineData("/tours/pt")]
    [InlineData("//pt/tours")]
    public void TryParse_rejects_a_missing_or_unsupported_first_segment(string path)
    {
        // Act
        var parsed = CulturePath.TryParse(new PathString(path), Supported, out var culture, out var remainder);

        // Assert
        parsed.ShouldBeFalse();
        culture.ShouldBeNull();
        remainder.HasValue.ShouldBeFalse();
    }

    [Theory]
    [InlineData("/tours/42", "/en/tours/42")]
    [InlineData("/", "/en/")]
    [InlineData("", "/en")]
    public void Prepend_adds_the_culture_segment(string path, string expected)
    {
        // Act
        var result = CulturePath.Prepend(new PathString(path), En, Supported);

        // Assert
        result.Value.ShouldBe(expected);
    }

    [Fact]
    public void Prepend_rejects_an_unsupported_culture()
    {
        // Act
        var prepend = () => { CulturePath.Prepend(new PathString("/tours"), Fr, Supported); };

        // Assert
        prepend.ShouldThrow<ArgumentException>();
    }

    [Theory]
    [InlineData("/pt/tours/42", "/en/tours/42")]
    [InlineData("/pt", "/en")]
    [InlineData("/pt/", "/en/")]
    [InlineData("/en/tours", "/en/tours")]
    [InlineData("/tours", "/en/tours")]
    [InlineData("/fr/tours", "/en/fr/tours")]
    public void Switch_replaces_or_adds_the_culture_segment(string path, string expected)
    {
        // Act
        var result = CulturePath.Switch(new PathString(path), En, Supported);

        // Assert
        result.Value.ShouldBe(expected);
    }

    [Fact]
    public void Null_arguments_are_caller_errors()
    {
        // Act
        var parse = () => { CulturePath.TryParse(new PathString("/pt"), null!, out _, out _); };
        var prependCultures = () => { CulturePath.Prepend(new PathString("/"), Pt, null!); };
        var prependCulture = () => { CulturePath.Prepend(new PathString("/"), null!, Supported); };
        var switchCultures = () => { CulturePath.Switch(new PathString("/"), Pt, null!); };

        // Assert
        parse.ShouldThrow<ArgumentNullException>();
        prependCultures.ShouldThrow<ArgumentNullException>();
        prependCulture.ShouldThrow<ArgumentNullException>();
        switchCultures.ShouldThrow<ArgumentNullException>();
    }
}
