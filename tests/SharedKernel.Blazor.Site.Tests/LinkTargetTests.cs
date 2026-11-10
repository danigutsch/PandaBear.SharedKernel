namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the scheme allow list and the shape rules of link targets.</summary>
public sealed class LinkTargetTests
{
    [Theory]
    [InlineData("/tours")]
    [InlineData("tours/one")]
    [InlineData("./a:b")]
    [InlineData("#top")]
    [InlineData("?page=2")]
    [InlineData("https://example.org/a")]
    [InlineData("HTTPS://example.org/a?b=c#d")]
    [InlineData("http://example.org/a")]
    [InlineData("mailto:hello@example.org")]
    [InlineData("tel:+5511999990000")]
    [InlineData("tel:5511999990000")]
    public void Safe_links_are_accepted(string href)
    {
        // Arrange & Act
        var result = LinkTarget.Create(href);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(href);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JaVaScRiPt:alert(1)")]
    [InlineData("data:text/html,x")]
    [InlineData("vbscript:x")]
    [InlineData("file:///etc/passwd")]
    [InlineData(" javascript:alert(1)")]
    [InlineData("java\tscript:alert(1)")]
    [InlineData("//evil.example/x")]
    [InlineData("\\\\evil.example\\x")]
    [InlineData("/\\evil.example")]
    [InlineData("/tours ")]
    [InlineData("/a b")]
    [InlineData("/a\u0085")]
    [InlineData("/a b")]
    [InlineData("a:b")]
    [InlineData("https:evil.example")]
    [InlineData("https:/evil.example")]
    [InlineData("https://")]
    [InlineData("https://user:pw@example.org/")]
    [InlineData("http://user@example.org/")]
    [InlineData("mailto:a@example.org?bcc=evil@example.org")]
    [InlineData("mailto:a@example.org&cc=evil@example.org")]
    [InlineData("mailto:a@example.org;b@example.org")]
    [InlineData("mailto:a@example.org,b@example.org")]
    [InlineData("mailto:a@example.org%0Abcc:evil@example.org")]
    [InlineData("mailto:example.org")]
    [InlineData("mailto:@example.org")]
    [InlineData("mailto:a@")]
    [InlineData("mailto:")]
    [InlineData("tel:")]
    [InlineData("tel:+")]
    [InlineData("tel:call-me")]
    [InlineData("tel:55+11")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Unsafe_links_are_invalid(string? href)
    {
        // Arrange & Act
        var result = LinkTarget.Create(href);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Links_longer_than_the_maximum_are_invalid()
    {
        // Arrange
        var atLimit = "/" + new string('a', LinkTarget.MaxLength - 1);
        var overLimit = atLimit + "a";

        // Act
        var accepted = LinkTarget.Create(atLimit);
        var rejected = LinkTarget.Create(overLimit);

        // Assert
        accepted.IsSuccess.ShouldBeTrue();
        rejected.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Validation_errors_do_not_echo_the_input()
    {
        // Arrange & Act
        var result = LinkTarget.Create("javascript:secret");

        // Assert
        result.ErrorDetails!.Detail!.ShouldNotContain("secret", StringComparison.Ordinal);
    }

    [Fact]
    public void From_string_throws_for_an_unsafe_link_and_keeps_a_safe_one()
    {
        // Arrange & Act
        var unsafeLink = () => LinkTarget.FromString("javascript:alert(1)");
        var safe = LinkTarget.FromString("/tours");

        // Assert
        unsafeLink.ShouldThrow<ArgumentException>();
        safe.ShouldBe(LinkTarget.FromString("/tours"));
        safe.ToString().ShouldBe("/tours");
    }

    [Theory]
    [InlineData("/img/a.jpg", true)]
    [InlineData("https://cdn.example.org/a.jpg", true)]
    [InlineData("http://cdn.example.org/a.jpg", false)]
    [InlineData("img/a.jpg", false)]
    [InlineData("#a", false)]
    [InlineData("mailto:a@example.org", false)]
    [InlineData("tel:+5511999990000", false)]
    public void Only_root_relative_and_https_targets_are_image_sources(string href, bool expected)
    {
        // Arrange
        var target = LinkTarget.FromString(href);

        // Act
        var isImageSource = target.IsImageSource;

        // Assert
        isImageSource.ShouldBe(expected);
    }

    [Fact]
    public void A_lone_surrogate_is_invalid_instead_of_throwing()
    {
        // Arrange
        // Lone surrogates are built in code: attribute arguments are stored as UTF-8 and would arrive as U+FFFD.
        string[] hrefs = ["/img/\uD800.jpg", "https://example.org/\uDC00", "http://example.org/\uD83D"];

        // Act
        var failures = hrefs.Select(static href => LinkTarget.Create(href).IsFailure).ToList();

        // Assert
        failures.ShouldBe([true, true, true]);
    }

    [Fact]
    public void An_http_address_with_an_empty_user_information_is_invalid()
    {
        // Arrange & Act
        var result = LinkTarget.Create("https://@example.org/");

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void An_absolute_address_keeps_its_original_case()
    {
        // Arrange & Act
        var target = LinkTarget.FromString("HTTPS://Example.ORG/A");

        // Assert
        target.Value.ShouldBe("HTTPS://Example.ORG/A");
        target.IsImageSource.ShouldBeTrue();
    }
}
