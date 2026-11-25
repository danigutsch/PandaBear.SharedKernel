using SharedKernel.Testing;

namespace SharedKernel.AspNetCore.Tests;

/// <summary>
/// Tests the canonical form of a moved path: requested and stored paths that differ only in case, trailing slash, escaping or Unicode
/// composition meet in one form, and anything that is not a plain absolute path is refused instead of being matched.
/// </summary>
[TestScope(TestScope.Unit)]
public sealed class PermanentRedirectPathsTests
{
    private const int MaxLength = 512;

    [Theory]
    [InlineData("/Viagens/Caminho/", "/viagens/caminho")]
    [InlineData("/viagens/caminho//", "/viagens/caminho")]
    [InlineData(" /caminho-portugues ", "/caminho-portugues")]
    [InlineData("/Quem-Somos/Hist%C3%B3ria/", "/quem-somos/história")]
    [InlineData("/quem-somos/hist%c3%b3ria", "/quem-somos/história")]
    [InlineData("/quem-somos/histo\u0301ria", "/quem-somos/história")]
    [InlineData("/quem-somos/HISTÓRIA", "/quem-somos/história")]
    [InlineData("/100%", "/100%")]
    [InlineData("/100%25", "/100%")]
    [InlineData("/a.b/c..d/...", "/a.b/c..d/...")]
    public void A_path_is_normalized_to_its_canonical_form(string path, string expected)
    {
        // Act
        var normalized = PermanentRedirectPaths.Normalize(path, MaxLength);

        // Assert
        normalized.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("caminho")]
    [InlineData("/")]
    [InlineData("//")]
    [InlineData("//evil.example.com")]
    [InlineData("/%2Fevil.example.com")]
    [InlineData("https://evil.example.com/")]
    [InlineData("/a//b")]
    [InlineData("/a?b=c")]
    [InlineData("/a%3Fb")]
    [InlineData("/a#b")]
    [InlineData("/a%23b")]
    [InlineData("/a\\b")]
    [InlineData("/a%5Cb")]
    [InlineData("/a b")]
    [InlineData("/a%20b")]
    [InlineData("/a%2Fb")]
    [InlineData("/a%2fb")]
    [InlineData("/a/../b")]
    [InlineData("/a/%2E%2E/b")]
    [InlineData("/a/%2e/b")]
    [InlineData("/./a")]
    [InlineData("/a/..")]
    [InlineData("/a\u0007b")]
    [InlineData("/a%00b")]
    [InlineData("/a%0D%0ALocation:%20x")]
    [InlineData("/a\u0085b")]
    [InlineData("/a\u00A0b")]
    [InlineData("/a\u2028b")]
    [InlineData("/a\u200Bb")]
    [InlineData("/a\u202Eb")]
    [InlineData("/a%E2%80%AEb")]
    [InlineData("/a\uFFFDb")]
    [InlineData("/a%252Fb")]
    [InlineData("/a%2541")]
    [InlineData("/a%C3")]
    public void A_path_that_is_not_a_plain_absolute_path_is_refused(string? path)
    {
        // Act
        var normalized = PermanentRedirectPaths.Normalize(path, MaxLength);

        // Assert
        normalized.ShouldBeNull();
    }

    [Fact]
    public void A_path_with_a_lone_surrogate_is_refused()
    {
        // Act
        var highOnly = PermanentRedirectPaths.Normalize("/a\uD800b", MaxLength);
        var lowOnly = PermanentRedirectPaths.Normalize("/a\uDC00", MaxLength);

        // Assert
        highOnly.ShouldBeNull();
        lowOnly.ShouldBeNull();
    }

    [Fact]
    public void A_canonical_path_at_the_limit_is_kept_and_one_character_more_is_refused()
    {
        // Arrange
        var atLimit = "/" + new string('a', MaxLength - 1);

        // Act
        var kept = PermanentRedirectPaths.Normalize(atLimit, MaxLength);
        var refused = PermanentRedirectPaths.Normalize(atLimit + "a", MaxLength);

        // Assert
        kept.ShouldBe(atLimit);
        refused.ShouldBeNull();
    }

    [Fact]
    public void A_percent_encoded_path_at_the_limit_is_kept()
    {
        // Arrange
        var encoded = "/" + string.Concat(Enumerable.Repeat("%E2%82%AC", MaxLength - 1));

        // Act
        var normalized = PermanentRedirectPaths.Normalize(encoded, MaxLength);

        // Assert
        normalized.ShouldBe("/" + new string('€', MaxLength - 1));
    }

    [Fact]
    public void An_over_long_input_is_refused_even_when_its_canonical_form_is_short()
    {
        // Arrange
        var padded = "/a" + new string('/', 9 * MaxLength);

        // Act
        var normalized = PermanentRedirectPaths.Normalize(padded, MaxLength);

        // Assert
        normalized.ShouldBeNull();
    }

    [Theory]
    [InlineData("/Viagens/Caminho/")]
    [InlineData("/Quem-Somos/Hist%C3%B3ria/")]
    [InlineData("/quem-somos/histo\u0301ria")]
    [InlineData("/100%25")]
    [InlineData("/pt/viagens/caminho-antigo")]
    public void A_canonical_path_normalizes_to_itself(string path)
    {
        // Arrange
        var canonical = PermanentRedirectPaths.Normalize(path, MaxLength).ShouldNotBeNull();

        // Act
        var again = PermanentRedirectPaths.Normalize(canonical, MaxLength);

        // Assert
        again.ShouldBe(canonical);
    }

    [Theory]
    [InlineData("/quem-somos/história", "/quem-somos/hist%C3%B3ria")]
    [InlineData("/pt/viagens/caminho", "/pt/viagens/caminho")]
    [InlineData("/100%", "/100%25")]
    [InlineData("/a~b_c.d-e", "/a~b_c.d-e")]
    public void A_canonical_path_is_percent_encoded_for_a_location_header(string path, string expected)
    {
        // Act
        var encoded = PermanentRedirectPaths.Encode(path);

        // Assert
        encoded.ShouldBe(expected);
    }

    [Theory]
    [InlineData("/quem-somos/história")]
    [InlineData("/100%")]
    [InlineData("/pt/viagens/caminho")]
    public void An_encoded_canonical_path_normalizes_back_to_it(string canonical)
    {
        // Arrange
        var encoded = PermanentRedirectPaths.Encode(canonical);

        // Act
        var normalized = PermanentRedirectPaths.Normalize(encoded, MaxLength);

        // Assert
        normalized.ShouldBe(canonical);
    }

    [Fact]
    public void Invalid_arguments_are_rejected()
    {
        // Act
        Action noPath = () => PermanentRedirectPaths.Encode(null!);
        Action tooSmallLimit = () => PermanentRedirectPaths.Normalize("/a", 1);

        // Assert
        noPath.ShouldThrow<ArgumentNullException>();
        tooSmallLimit.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
