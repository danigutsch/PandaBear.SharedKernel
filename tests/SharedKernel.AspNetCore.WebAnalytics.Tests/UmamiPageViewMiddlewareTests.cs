using System.Net;
using System.Text.Json;
using PandaBear.Testing.Http;

namespace PandaBear.AspNetCore.WebAnalytics.Tests;

/// <summary>Tests the forwarder in a running site: what reaches Umami for a page view, and that Umami or the website lookup can never fail or change a page.</summary>
public sealed class UmamiPageViewMiddlewareTests
{
    private const string TransportFailureMessage = "transport-detail";

    [Fact]
    public async Task A_page_view_is_posted_to_umami_with_the_visitors_browser_and_address()
    {
        // Arrange
        using var umami = new RecordingHttpMessageHandler();
        await using var site = await PageViewTestApp.Start(umami);

        // Act
        using var page = await site.Visit(
            HttpMethod.Get,
            "/pt/viagens?utm_source=newsletter&email=visitor%40example.com",
            ("Accept-Language", "pt-BR,pt;q=0.9,en;q=0.8"),
            ("Referer", "https://www.google.com/search?q=private+words"));
        var arrived = await umami.WaitForRequests(1, TestContext.Current.CancellationToken);
        var sent = arrived.ShouldHaveSingleItem();

        // Assert
        page.StatusCode.ShouldBe(HttpStatusCode.OK);
        sent.Method.ShouldBe(HttpMethod.Post);
        sent.Message.RequestUri.ShouldBe(new Uri("http://umami.test/api/send"));
        sent.Message.Headers.NonValidated["User-Agent"].ToString().ShouldBe(PageContexts.VisitorAgent);
        sent.Message.Headers.NonValidated["X-Forwarded-For"].ToString().ShouldBe("203.0.113.7");
        sent.Message.Content.ShouldNotBeNull().Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("application/json");
        using var body = JsonDocument.Parse(sent.Body.ShouldNotBeNull());
        UmamiBodies.PropertyNames(body.RootElement).ShouldBe(["payload", "type"]);
        body.RootElement.GetProperty("type").GetString().ShouldBe("event");
        var payload = body.RootElement.GetProperty("payload");
        UmamiBodies.PropertyNames(payload).ShouldBe(["hostname", "language", "referrer", "url", "website"]);
        payload.GetProperty("website").GetString().ShouldBe("8f1d8a3e-7c2b-4d5e-9f60-1a2b3c4d5e6f");
        payload.GetProperty("hostname").GetString().ShouldBe("localhost");
        payload.GetProperty("url").GetString().ShouldBe("/pt/viagens?utm_source=newsletter");
        payload.GetProperty("referrer").GetString().ShouldBe("https://www.google.com/search");
        payload.GetProperty("language").GetString().ShouldBe("pt-BR");
    }

    [Fact]
    public async Task The_payload_leaves_out_what_the_request_does_not_carry()
    {
        // Arrange
        using var umami = new RecordingHttpMessageHandler();
        await using var site = await PageViewTestApp.Start(umami, configure: options => options.Hostname = "viajantes.example");

        // Act
        using var page = await site.Visit(HttpMethod.Get, "/en/tours");
        var arrived = await umami.WaitForRequests(1, TestContext.Current.CancellationToken);
        var sent = arrived.ShouldHaveSingleItem();

        // Assert
        using var body = JsonDocument.Parse(sent.Body.ShouldNotBeNull());
        var payload = body.RootElement.GetProperty("payload");
        UmamiBodies.PropertyNames(payload).ShouldBe(["hostname", "url", "website"], "a page view is an event without a name, and the server knows no title or screen");
        payload.GetProperty("hostname").GetString().ShouldBe("viajantes.example");
    }

    [Theory]
    [InlineData("http://umami.test/stats", "http://umami.test/stats/api/send")]
    [InlineData("https://analytics.example.com/umami/", "https://analytics.example.com/umami/api/send")]
    public async Task The_send_address_keeps_the_path_of_the_endpoint(string endpoint, string expected)
    {
        // Arrange
        using var umami = new RecordingHttpMessageHandler();
        await using var site = await PageViewTestApp.Start(umami, endpoint);

        // Act
        using var page = await site.Visit(HttpMethod.Get, "/pt");
        var arrived = await umami.WaitForRequests(1, TestContext.Current.CancellationToken);
        var sent = arrived.ShouldHaveSingleItem();

        // Assert
        sent.Message.RequestUri.ShouldBe(new Uri(expected));
    }

    [Fact]
    public async Task The_address_goes_in_the_configured_header_only()
    {
        // Arrange
        using var umami = new RecordingHttpMessageHandler();
        await using var site = await PageViewTestApp.Start(umami, configure: options => options.ClientIpHeader = "X-Visitor-IP");

        // Act
        using var page = await site.Visit(HttpMethod.Get, "/pt");
        var arrived = await umami.WaitForRequests(1, TestContext.Current.CancellationToken);
        var sent = arrived.ShouldHaveSingleItem();

        // Assert
        sent.Message.Headers.NonValidated["X-Visitor-IP"].ToString().ShouldBe("203.0.113.7");
        sent.Message.Headers.Contains("X-Forwarded-For").ShouldBeFalse();
    }

    [Fact]
    public async Task Requests_that_are_not_page_views_send_nothing()
    {
        // Arrange
        using var umami = new RecordingHttpMessageHandler();
        await using var site = await PageViewTestApp.Start(umami);

        // Act
        (await site.Visit(HttpMethod.Head, "/pt")).Dispose();
        (await site.Visit(HttpMethod.Post, "/form")).Dispose();
        (await site.Visit(HttpMethod.Get, "/site.css")).Dispose();
        (await site.Visit(HttpMethod.Get, "/missing")).Dispose();
        (await site.Visit(HttpMethod.Get, "/moved")).Dispose();
        (await site.Visit(HttpMethod.Get, "/pt", ("Sec-Purpose", "prefetch;prerender"))).Dispose();
        (await site.Visit(HttpMethod.Get, "/pt", ("Purpose", "prefetch"))).Dispose();
        (await site.Visit(HttpMethod.Get, "/pt", ("Sec-GPC", "1"))).Dispose();
        (await site.Visit(HttpMethod.Get, "/pt", ("DNT", "1"))).Dispose();
        (await site.Visit(HttpMethod.Get, FixedWebsiteIdProvider.UntrackedPath)).Dispose();
        (await site.Visit(HttpMethod.Get, "/marker")).Dispose();
        var arrived = await umami.WaitForRequests(1, TestContext.Current.CancellationToken);

        // Assert
        var firstUrl = UmamiBodies.UrlOf(arrived.ShouldHaveSingleItem().Body);
        firstUrl.ShouldBe("/marker", "page views are sent in order, so anything recorded earlier would have arrived first");
    }

    [Fact]
    public async Task A_failing_umami_never_changes_the_page_and_is_logged_without_visitor_data()
    {
        // Arrange
        var calls = 0;
        using var umami = new RecordingHttpMessageHandler(_ => Interlocked.Increment(ref calls) switch
        {
            1 => new HttpResponseMessage(HttpStatusCode.InternalServerError),
            2 => throw new HttpRequestException(TransportFailureMessage),
            _ => new HttpResponseMessage(HttpStatusCode.OK),
        });
        await using var site = await PageViewTestApp.Start(umami);

        // Act
        using var first = await site.Visit(HttpMethod.Get, VisitorDataAssertions.SecretPath, ("Referer", $"https://{VisitorDataAssertions.SecretReferrerHost}/inbox"));
        using var second = await site.Visit(HttpMethod.Get, VisitorDataAssertions.SecretPath);
        using var third = await site.Visit(HttpMethod.Get, VisitorDataAssertions.SecretPath);
        await umami.WaitForRequests(3, TestContext.Current.CancellationToken);

        // Assert
        foreach (var page in new[] { first, second, third })
        {
            page.StatusCode.ShouldBe(HttpStatusCode.OK);
            var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            html.ShouldBe(PageViewTestApp.PageHtml);
        }

        var warnings = site.Logs.Records.Where(record => record.EventId.Id is WebAnalyticsTelemetry.SendRejectedEventId or WebAnalyticsTelemetry.SendFailedEventId).ToArray();
        warnings.Length.ShouldBe(2);
        warnings.ShouldAllSatisfy(warning => warning.Level.ShouldBe(Microsoft.Extensions.Logging.LogLevel.Warning));
        warnings.ShouldAllSatisfy(warning => warning.Exception.ShouldBeNull());
        warnings.ShouldAllSatisfy(warning => warning.Message.ShouldNotContain(TransportFailureMessage, StringComparison.Ordinal));
        site.Logs.Records.ShouldAllSatisfy(record => record.ShouldHoldNoVisitorData());
        site.Meters.Measurements.Where(measurement => measurement.Instrument == WebAnalyticsTelemetry.DroppedPageViewsCounterName)
            .Select(measurement => (string?)measurement.Tags[WebAnalyticsTelemetry.ReasonTag])
            .Order(StringComparer.Ordinal)
            .ToArray()
            .ShouldBe([WebAnalyticsTelemetry.RejectedReason, WebAnalyticsTelemetry.SendFailedReason]);
    }

    [Fact]
    public async Task A_failing_website_lookup_never_changes_the_page_and_is_logged_without_its_message()
    {
        // Arrange
        using var umami = new RecordingHttpMessageHandler();
        await using var site = await PageViewTestApp.Start(umami);

        // Act
        using var page = await site.Visit(HttpMethod.Get, FixedWebsiteIdProvider.FailingPath);

        // Assert
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        page.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldBe(PageViewTestApp.PageHtml);
        var warning = site.Logs.Records.ShouldHaveSingleItem(record => record.EventId.Id == WebAnalyticsTelemetry.WebsiteIdLookupFailedEventId);
        warning.Level.ShouldBe(Microsoft.Extensions.Logging.LogLevel.Warning);
        warning.Message.ShouldContain(nameof(InvalidOperationException), StringComparison.Ordinal);
        warning.Exception.ShouldBeNull();
        site.Logs.Records.ShouldAllSatisfy(record => record.Message.ShouldNotContain(FixedWebsiteIdProvider.FailureMessage, StringComparison.Ordinal));
        umami.Requests.ShouldBeEmpty();
    }
}
