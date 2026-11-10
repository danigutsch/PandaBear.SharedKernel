namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the scheme allow list of link targets.</summary>
public sealed class LinkTargetTests
{
    [Theory]
    [InlineData("/tours")]
    [InlineData("tours/one")]
    [InlineData("#top")]
    [InlineData("?page=2")]
    [InlineData("https://example.org/a")]
    [InlineData("http://example.org/a")]
    [InlineData("mailto:hello@example.org")]
    [InlineData("tel:+5511999990000")]
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
    public void Text_conversion_throws_for_an_unsafe_link_and_keeps_a_safe_one()
    {
        // Arrange & Act
        Func<object?> unsafeLink = () => (LinkTarget)"javascript:alert(1)";
        LinkTarget safe = "/tours";

        // Assert
        unsafeLink.ShouldThrow<ArgumentException>();
        safe.ShouldBe(LinkTarget.FromString("/tours"));
        safe.ToString().ShouldBe("/tours");
    }
}
