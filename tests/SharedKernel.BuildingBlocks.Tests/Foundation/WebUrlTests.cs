using PandaBear.Results;

namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Tests the absolute http or https web address value object.</summary>
[Trait("Scope", "Unit")]
[Trait("Capability", "ValueObjects")]
public sealed class WebUrlTests
{
    [Theory]
    [InlineData("https://forms.gle/abc123", "https://forms.gle/abc123")]
    [InlineData("http://example.com/path?q=1", "http://example.com/path?q=1")]
    [InlineData("https://example.com", "https://example.com")]
    [InlineData("HTTPS://Example.ORG/Path?Q=1#Frag", "https://example.org/Path?Q=1#Frag")]
    [InlineData("Http://EXAMPLE.org:8080/a", "http://example.org:8080/a")]
    [InlineData("https://Example.org?q=A", "https://example.org?q=A")]
    [InlineData("https://Example.org#Top", "https://example.org#Top")]
    [InlineData("https://[2001:DB8::1]/x", "https://[2001:db8::1]/x")]
    [InlineData("https://bücher.example/a", "https://bücher.example/a")]
    [InlineData("https://example.org/a%20b", "https://example.org/a%20b")]
    public void Absolute_HTTP_and_https_addresses_are_accepted_with_a_lower_case_scheme_and_host(string input, string expected)
    {
        // Act
        var url = WebUrl.Create(input).ShouldBeSuccess();

        // Assert
        url.Value.ShouldBe(expected);
        url.ToString().ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("javascript:alert(1)")]
    [InlineData("JaVaScRiPt:alert(1)")]
    [InlineData("javascript://example.com/%0Aalert(1)")]
    [InlineData("data:text/html,hi")]
    [InlineData("vbscript:x")]
    [InlineData("mailto:a@example.org")]
    [InlineData("tel:+5511999990000")]
    [InlineData("ftp://example.com")]
    [InlineData("file:///etc/passwd")]
    [InlineData("httpx://example.org")]
    [InlineData("//evil.example.com")]
    [InlineData("/relative/path")]
    [InlineData("relative/path")]
    [InlineData("example.org")]
    [InlineData("#top")]
    [InlineData("https:evil.example")]
    [InlineData("https:/evil.example")]
    [InlineData("https://")]
    [InlineData("https:///path")]
    [InlineData("https://user:pw@example.org/")]
    [InlineData("http://user@example.org/")]
    [InlineData("https://@example.org/")]
    [InlineData("https://exa mple.com")]
    [InlineData(" https://example.com")]
    [InlineData("https://example.com ")]
    [InlineData("java\tscript:alert(1)")]
    [InlineData("https://example.com/\nx")]
    [InlineData("https://example.com/\0x")]
    [InlineData("https://example.com/a\u0085")]
    [InlineData("https://example.com/a b")]
    [InlineData("https://example.com\\@evil.example")]
    [InlineData("https:\\\\evil.example")]
    [InlineData("https://example.org:99999/")]
    public void Anything_but_an_absolute_HTTP_or_https_address_is_rejected_without_echoing_it(string? input)
    {
        // Act
        var result = WebUrl.Create(input, "signUpLink", "Sign-up link");

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        var message = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["signUpLink"].ShouldHaveSingleItem();
        message.ShouldStartWith("Sign-up link ", StringComparison.Ordinal);
        var echoed = !string.IsNullOrWhiteSpace(input) && message.Contains(input, StringComparison.Ordinal);
        echoed.ShouldBeFalse();
    }

    [Fact]
    public void Text_that_is_not_in_canonical_composed_form_is_rejected()
    {
        // Arrange
        // "e" followed by a combining acute accent; the composed form is "é". Built in code so the source encoding cannot compose it.
        var decomposed = "https://example.org/café";

        // Act
        var result = WebUrl.Create(decomposed);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
    }

    [Fact]
    public void A_lone_surrogate_is_rejected_instead_of_throwing()
    {
        // Arrange
        // Lone surrogates are built in code: attribute arguments are stored as UTF-8 and would arrive as U+FFFD.
        string[] inputs = ["https://example.org/\uD800", "https://example.org/\uDC00a", "https://\uD83Dexample.org/"];

        // Act
        var statuses = inputs.Select(static input => WebUrl.Create(input).Status).ToList();

        // Assert
        statuses.ShouldBe([ResultStatus.Invalid, ResultStatus.Invalid, ResultStatus.Invalid]);
    }

    [Fact]
    public void Default_field_and_label_name_the_error()
    {
        // Act
        var result = WebUrl.Create("javascript:alert(1)");

        // Assert
        var message = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["url"].ShouldHaveSingleItem();
        message.ShouldStartWith("Link ", StringComparison.Ordinal);
    }

    [Fact]
    public void An_address_longer_than_the_maximum_is_rejected()
    {
        // Arrange
        const string prefix = "https://example.com/";
        var atLimit = prefix + new string('a', WebUrl.MaxLength - prefix.Length);
        var overLimit = atLimit + "a";

        // Act
        var accepted = WebUrl.Create(atLimit);
        var rejected = WebUrl.Create(overLimit);

        // Assert
        accepted.ShouldBeSuccess().Value.ShouldBe(atLimit);
        rejected.Status.ShouldBe(ResultStatus.Invalid);
        rejected.ErrorDetails.ShouldNotBeNull().Detail.ShouldNotBeNull().ShouldContain(WebUrl.MaxLength.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public void Addresses_that_differ_only_in_scheme_or_host_case_are_equal()
    {
        // Act
        var upper = WebUrl.Create("HTTPS://EXAMPLE.org/a").ShouldBeSuccess();
        var lower = WebUrl.Create("https://example.org/a").ShouldBeSuccess();
        var otherPath = WebUrl.Create("https://example.org/A").ShouldBeSuccess();

        // Assert
        upper.ShouldBe(lower);
        upper.ShouldNotBe(otherPath);
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=abc123", "https://www.youtube.com/watch?v=abc123")]
    [InlineData("HTTPS://Vimeo.COM/1", "https://vimeo.com/1")]
    public void An_https_only_address_accepts_https_with_the_same_canonical_form(string input, string expected)
    {
        // Act
        var url = WebUrl.CreateHttps(input).ShouldBeSuccess();

        // Assert
        url.Value.ShouldBe(expected);
        url.ShouldBe(WebUrl.Create(input).ShouldBeSuccess());
    }

    [Theory]
    [InlineData("http://www.youtube.com/watch?v=abc123")]
    [InlineData("HTTP://example.org")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.org")]
    [InlineData("//example.org")]
    [InlineData("/videos/1")]
    public void An_https_only_address_rejects_HTTP_and_every_other_scheme_without_echoing_it(string input)
    {
        // Act
        var result = WebUrl.CreateHttps(input, "videoUrl", "Video address");

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        var message = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["videoUrl"].ShouldHaveSingleItem();
        message.ShouldBe("Video address must be an absolute https address.");
        result.ErrorDetails.Detail.ShouldBe(message);
    }

    [Theory]
    [InlineData(null, "Link is required.")]
    [InlineData("https://user@example.org/", "Link must be an absolute https address with a host and no user information.")]
    [InlineData("https://exa mple.org", "Link cannot contain white space, control characters or backslashes.")]
    public void An_https_only_address_applies_every_other_rule_of_a_web_address(string? input, string expected)
    {
        // Act
        var result = WebUrl.CreateHttps(input);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["url"].ShouldBe([expected]);
    }

    [Fact]
    public void Restore_rebuilds_a_stored_address()
    {
        // Act
        var url = WebUrl.Restore("https://example.org/a");

        // Assert
        url.Value.ShouldBe("https://example.org/a");
        url.ShouldBe(WebUrl.Create("https://example.org/a").ShouldBeSuccess());
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Restore_rejects_a_blank_value(string? value)
    {
        // Act
        var restore = () => { WebUrl.Restore(value!); };

        // Assert
        restore.ShouldThrow<ArgumentException>();
    }
}
