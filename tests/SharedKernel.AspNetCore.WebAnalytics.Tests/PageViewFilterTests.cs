using Microsoft.AspNetCore.Http;

namespace PandaBear.AspNetCore.WebAnalytics.Tests;

/// <summary>Tests which answered requests count as page views: only a GET answered 200 with HTML, never a prefetch, and not when the visitor asks not to be tracked.</summary>
public sealed class PageViewFilterTests
{
    [Fact]
    public void A_get_answered_200_with_HTML_is_a_page_view()
    {
        // Arrange
        var context = PageContexts.AnsweredPage();

        // Act
        var isPageView = PageViewCapture.IsPageView(context, honorPrivacySignals: true);

        // Assert
        isPageView.ShouldBeTrue();
    }

    [Theory]
    [InlineData("HEAD")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("OPTIONS")]
    public void Other_methods_than_get_are_not_page_views(string method)
    {
        // Arrange
        var context = PageContexts.AnsweredPage();
        context.Request.Method = method;

        // Act
        var isPageView = PageViewCapture.IsPageView(context, honorPrivacySignals: true);

        // Assert
        isPageView.ShouldBeFalse();
    }

    [Theory]
    [InlineData(StatusCodes.Status201Created)]
    [InlineData(StatusCodes.Status204NoContent)]
    [InlineData(StatusCodes.Status206PartialContent)]
    [InlineData(StatusCodes.Status301MovedPermanently)]
    [InlineData(StatusCodes.Status302Found)]
    [InlineData(StatusCodes.Status304NotModified)]
    [InlineData(StatusCodes.Status400BadRequest)]
    [InlineData(StatusCodes.Status404NotFound)]
    [InlineData(StatusCodes.Status500InternalServerError)]
    public void Answers_other_than_200_are_not_page_views(int statusCode)
    {
        // Arrange
        var context = PageContexts.AnsweredPage();
        context.Response.StatusCode = statusCode;

        // Act
        var isPageView = PageViewCapture.IsPageView(context, honorPrivacySignals: true);

        // Assert
        isPageView.ShouldBeFalse();
    }

    [Theory]
    [InlineData("text/css")]
    [InlineData("text/javascript")]
    [InlineData("text/plain; charset=utf-8")]
    [InlineData("application/xml")]
    [InlineData("application/json")]
    [InlineData("image/webp")]
    [InlineData("font/woff2")]
    [InlineData("text/htmlx")]
    [InlineData("not a media type")]
    public void Answers_that_are_not_HTML_are_not_page_views(string contentType)
    {
        // Arrange
        var context = PageContexts.AnsweredPage();
        context.Response.ContentType = contentType;

        // Act
        var isPageView = PageViewCapture.IsPageView(context, honorPrivacySignals: true);

        // Assert
        isPageView.ShouldBeFalse();
    }

    [Fact]
    public void An_answer_without_a_content_type_is_not_a_page_view()
    {
        // Arrange
        var context = PageContexts.AnsweredPage();
        context.Response.ContentType = null;

        // Act
        var isPageView = PageViewCapture.IsPageView(context, honorPrivacySignals: true);

        // Assert
        isPageView.ShouldBeFalse();
    }

    [Theory]
    [InlineData("text/html")]
    [InlineData("TEXT/HTML; charset=UTF-8")]
    public void HTML_is_recognised_whatever_its_case_and_parameters(string contentType)
    {
        // Arrange
        var context = PageContexts.AnsweredPage();
        context.Response.ContentType = contentType;

        // Act
        var isPageView = PageViewCapture.IsPageView(context, honorPrivacySignals: true);

        // Assert
        isPageView.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Sec-Purpose", "prefetch")]
    [InlineData("Sec-Purpose", "prefetch;prerender")]
    [InlineData("Sec-Purpose", "prefetch; anonymous-client-ip")]
    [InlineData("Purpose", "prefetch")]
    [InlineData("Purpose", "Prefetch")]
    public void Prefetch_and_prerender_requests_are_not_page_views(string header, string value)
    {
        // Arrange
        var context = PageContexts.AnsweredPage();
        context.Request.Headers[header] = value;

        // Act
        var isPageView = PageViewCapture.IsPageView(context, honorPrivacySignals: true);

        // Assert
        isPageView.ShouldBeFalse();
    }

    [Theory]
    [InlineData("Sec-GPC", "1")]
    [InlineData("DNT", "1")]
    public void A_visitor_who_asks_not_to_be_tracked_is_not_counted(string header, string value)
    {
        // Arrange
        var context = PageContexts.AnsweredPage();
        context.Request.Headers[header] = value;

        // Act
        var isPageView = PageViewCapture.IsPageView(context, honorPrivacySignals: true);

        // Assert
        isPageView.ShouldBeFalse();
    }

    [Theory]
    [InlineData("Sec-GPC", "0")]
    [InlineData("DNT", "0")]
    [InlineData("DNT", "null")]
    public void A_privacy_signal_that_is_not_set_to_one_does_not_stop_counting(string header, string value)
    {
        // Arrange
        var context = PageContexts.AnsweredPage();
        context.Request.Headers[header] = value;

        // Act
        var isPageView = PageViewCapture.IsPageView(context, honorPrivacySignals: true);

        // Assert
        isPageView.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Sec-GPC")]
    [InlineData("DNT")]
    public void Privacy_signals_are_ignored_when_the_host_turns_them_off(string header)
    {
        // Arrange
        var context = PageContexts.AnsweredPage();
        context.Request.Headers[header] = "1";

        // Act
        var isPageView = PageViewCapture.IsPageView(context, honorPrivacySignals: false);

        // Assert
        isPageView.ShouldBeTrue();
    }

    [Fact]
    public void A_request_without_a_user_agent_is_not_a_page_view()
    {
        // Arrange
        var context = PageContexts.AnsweredPage();
        context.Request.Headers.UserAgent = string.Empty;

        // Act
        var isPageView = PageViewCapture.IsPageView(context, honorPrivacySignals: true);

        // Assert
        isPageView.ShouldBeFalse("Umami does not register a request without a User-Agent, and the server's own would be wrong");
    }
}
