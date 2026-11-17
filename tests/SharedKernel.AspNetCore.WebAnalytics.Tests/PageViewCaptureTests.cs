using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http;

namespace PandaBear.AspNetCore.WebAnalytics.Tests;

/// <summary>Tests what a page view keeps of the request: the path with only the <c>utm_*</c> query values, the referrer without query or fragment, the first language, the host, the browser and the shortened address.</summary>
public sealed class PageViewCaptureTests
{
    [Fact]
    public void The_path_and_query_is_the_path_when_the_request_has_no_query()
    {
        // Arrange
        var context = PageContexts.AnsweredPage("/pt/viagens/patagonia");

        // Act
        var pageView = PageViewCapture.Capture(context, configuredHostname: null);

        // Assert
        pageView.PathAndQuery.ShouldBe("/pt/viagens/patagonia");
    }

    [Fact]
    public void Only_utm_query_values_are_kept_and_every_other_query_value_is_dropped()
    {
        // Arrange
        var context = PageContexts.AnsweredPage("/pt/viagens?email=visitor%40example.com&utm_source=newsletter&gclid=abc&utm_campaign=spring%20sale&token=secret");

        // Act
        var pageView = PageViewCapture.Capture(context, configuredHostname: null);

        // Assert
        pageView.PathAndQuery.ShouldBe("/pt/viagens?utm_campaign=spring%20sale&utm_source=newsletter");
    }

    [Fact]
    public void Query_keys_that_only_look_like_utm_keys_are_dropped()
    {
        // Arrange
        var context = PageContexts.AnsweredPage("/en/tours?UTM_SOURCE=upper&utmsource=joined&xutm_source=prefixed");

        // Act
        var pageView = PageViewCapture.Capture(context, configuredHostname: null);

        // Assert
        pageView.PathAndQuery.ShouldBe("/en/tours");
    }

    [Fact]
    public void The_path_and_query_keeps_the_path_base_and_the_escaping_of_the_path()
    {
        // Arrange
        var context = PageContexts.AnsweredPage();
        context.Request.PathBase = "/site";
        context.Request.Path = "/pt/viagens/são-tomé";

        // Act
        var pageView = PageViewCapture.Capture(context, configuredHostname: null);

        // Assert
        pageView.PathAndQuery.ShouldBe("/site/pt/viagens/s%C3%A3o-tom%C3%A9");
    }

    [Theory]
    [InlineData("https://www.google.com/search?q=private+words#top", "https://www.google.com/search")]
    [InlineData("https://user:password@blog.example.org/post?id=7", "https://blog.example.org/post")]
    [InlineData("http://news.example.net:8080/a/b", "http://news.example.net:8080/a/b")]
    [InlineData("https://duckduckgo.com/", "https://duckduckgo.com/")]
    public void An_external_referrer_is_kept_without_its_query_fragment_and_credentials(string referer, string expected)
    {
        // Arrange
        var context = PageContexts.AnsweredPage();
        context.Request.Headers.Referer = referer;

        // Act
        var pageView = PageViewCapture.Capture(context, configuredHostname: null);

        // Assert
        pageView.Referrer.ShouldBe(expected);
    }

    [Theory]
    [InlineData("https://www.example.com/pt?utm_source=x")]
    [InlineData("https://example.com/en/tours")]
    [InlineData("http://WWW.EXAMPLE.COM:8443/pt/galeria")]
    public void A_referrer_on_the_same_site_is_not_sent(string referer)
    {
        // Arrange
        var context = PageContexts.AnsweredPage();
        context.Request.Headers.Referer = referer;

        // Act
        var pageView = PageViewCapture.Capture(context, configuredHostname: null);

        // Assert
        pageView.Referrer.ShouldBeNull();
    }

    [Fact]
    public void A_referrer_on_the_configured_host_is_not_sent()
    {
        // Arrange
        var context = PageContexts.AnsweredPage();
        context.Request.Host = new HostString("internal-node:8080");
        context.Request.Headers.Referer = "https://viajantes.example/pt";

        // Act
        var pageView = PageViewCapture.Capture(context, configuredHostname: "viajantes.example");

        // Assert
        pageView.Referrer.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("/relative/path")]
    [InlineData("android-app://com.google.android.gm/")]
    [InlineData("file:///etc/passwd")]
    [InlineData("not a url")]
    public void A_missing_relative_or_non_web_referrer_is_not_sent(string referer)
    {
        // Arrange
        var context = PageContexts.AnsweredPage();
        context.Request.Headers.Referer = referer;

        // Act
        var pageView = PageViewCapture.Capture(context, configuredHostname: null);

        // Assert
        pageView.Referrer.ShouldBeNull();
    }

    [Theory]
    [InlineData("pt-BR,pt;q=0.9,en;q=0.8", "pt-BR")]
    [InlineData("en;q=0.5, pt-PT", "en")]
    [InlineData(" de-CH ", "de-CH")]
    [InlineData("zh-Hant-TW", "zh-Hant-TW")]
    public void The_language_is_the_first_accept_language_tag(string acceptLanguage, string expected)
    {
        // Arrange
        var context = PageContexts.AnsweredPage();
        context.Request.Headers.AcceptLanguage = acceptLanguage;

        // Act
        var pageView = PageViewCapture.Capture(context, configuredHostname: null);

        // Assert
        pageView.Language.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("*")]
    [InlineData("<script>")]
    [InlineData("en-")]
    [InlineData("-en")]
    [InlineData("averyveryverylongprimarytag")]
    public void A_missing_wildcard_or_malformed_language_is_not_sent(string acceptLanguage)
    {
        // Arrange
        var context = PageContexts.AnsweredPage();
        context.Request.Headers.AcceptLanguage = acceptLanguage;

        // Act
        var pageView = PageViewCapture.Capture(context, configuredHostname: null);

        // Assert
        pageView.Language.ShouldBeNull();
    }

    [Fact]
    public void The_hostname_is_the_request_host_without_its_port()
    {
        // Arrange
        var context = PageContexts.AnsweredPage();
        context.Request.Host = new HostString("www.example.com", 8443);

        // Act
        var pageView = PageViewCapture.Capture(context, configuredHostname: null);

        // Assert
        pageView.Hostname.ShouldBe("www.example.com");
    }

    [Fact]
    public void A_configured_hostname_replaces_the_request_host()
    {
        // Arrange
        var context = PageContexts.AnsweredPage();
        context.Request.Host = new HostString("10.0.0.5", 8080);

        // Act
        var pageView = PageViewCapture.Capture(context, configuredHostname: "viajantes.example");

        // Assert
        pageView.Hostname.ShouldBe("viajantes.example");
    }

    [Fact]
    public void The_visitors_browser_and_shortened_address_are_kept_for_the_targets()
    {
        // Arrange
        var context = PageContexts.AnsweredPage();

        // Act
        var pageView = PageViewCapture.Capture(context, configuredHostname: null);

        // Assert
        pageView.UserAgent.ShouldBe(PageContexts.VisitorAgent);
        pageView.ClientIp.ShouldBe(PageContexts.ShortenedVisitorAddress);
    }

    [Theory]
    [InlineData("203.0.113.7", "203.0.113.0")]
    [InlineData("198.51.100.255", "198.51.100.0")]
    [InlineData("192.0.2.0", "192.0.2.0")]
    public void An_ipv4_address_keeps_its_first_24_bits_and_loses_the_last_octet(string address, string expected)
    {
        // Arrange
        var context = PageContexts.AnsweredPage();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);

        // Act
        var pageView = PageViewCapture.Capture(context, configuredHostname: null);

        // Assert
        var shortened = pageView.ClientIp.ShouldNotBeNull();
        shortened.AddressFamily.ShouldBe(AddressFamily.InterNetwork);
        shortened.ToString().ShouldBe(expected);
    }

    [Theory]
    [InlineData("2001:db8:85a3:8d3:1319:8a2e:370:7348", "2001:db8:85a3::")]
    [InlineData("2001:db8:85a3:ffff:ffff:ffff:ffff:ffff", "2001:db8:85a3::")]
    [InlineData("2001:db8::7", "2001:db8::")]
    [InlineData("fe80::1%3", "fe80::")]
    public void An_ipv6_address_keeps_its_first_48_bits_and_no_scope(string address, string expected)
    {
        // Arrange
        var context = PageContexts.AnsweredPage();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);

        // Act
        var pageView = PageViewCapture.Capture(context, configuredHostname: null);

        // Assert
        var shortened = pageView.ClientIp.ShouldNotBeNull();
        shortened.AddressFamily.ShouldBe(AddressFamily.InterNetworkV6);
        shortened.ScopeId.ShouldBe(0);
        shortened.ToString().ShouldBe(expected);
    }

    [Theory]
    [InlineData("::ffff:198.51.100.9", "198.51.100.0")]
    [InlineData("::ffff:203.0.113.255", "203.0.113.0")]
    public void An_ipv4_mapped_ipv6_address_is_shortened_as_its_ipv4_address(string address, string expected)
    {
        // Arrange
        var context = PageContexts.AnsweredPage();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);

        // Act
        var pageView = PageViewCapture.Capture(context, configuredHostname: null);

        // Assert
        var shortened = pageView.ClientIp.ShouldNotBeNull();
        shortened.AddressFamily.ShouldBe(AddressFamily.InterNetwork);
        shortened.ToString().ShouldBe(expected);
    }

    [Fact]
    public void A_request_without_an_address_has_none()
    {
        // Arrange
        var context = PageContexts.AnsweredPage();
        context.Connection.RemoteIpAddress = null;

        // Act
        var pageView = PageViewCapture.Capture(context, configuredHostname: null);

        // Assert
        pageView.ClientIp.ShouldBeNull();
    }
}
