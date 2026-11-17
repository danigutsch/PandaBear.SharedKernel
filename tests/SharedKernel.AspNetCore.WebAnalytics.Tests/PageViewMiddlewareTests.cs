using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PandaBear.Testing.Http;

namespace PandaBear.AspNetCore.WebAnalytics.Tests;

/// <summary>Tests page views in a running site: what reaches Umami and every other target, and that no target can ever fail or change a page.</summary>
public sealed class PageViewMiddlewareTests
{
    private const string TransportFailureMessage = "transport-detail";

    [Fact]
    public async Task A_page_view_is_posted_to_umami_with_the_visitors_browser_and_shortened_address()
    {
        // Arrange
        using var umami = new RecordingHttpMessageHandler();
        await using var site = await PageViewTestApp.Start(umami, UmamiConnections.Default);

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
        sent.Message.Headers.NonValidated["X-Forwarded-For"].ToString().ShouldBe("203.0.113.0");
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
        await using var site = await PageViewTestApp.Start(umami, UmamiConnections.Default, options => options.Hostname = "viajantes.example");

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
        await using var site = await PageViewTestApp.Start(umami, UmamiConnections.For(endpoint));

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
        await using var site = await PageViewTestApp.Start(umami, UmamiConnections.Default, configureUmami: options => options.ClientIpHeader = "X-Visitor-IP");

        // Act
        using var page = await site.Visit(HttpMethod.Get, "/pt");
        var arrived = await umami.WaitForRequests(1, TestContext.Current.CancellationToken);
        var sent = arrived.ShouldHaveSingleItem();

        // Assert
        sent.Message.Headers.NonValidated["X-Visitor-IP"].ToString().ShouldBe("203.0.113.0");
        sent.Message.Headers.Contains("X-Forwarded-For").ShouldBeFalse();
    }

    [Theory]
    [InlineData("203.0.113.7", "203.0.113.0")]
    [InlineData("2001:db8:85a3:8d3:1319:8a2e:370:7348", "2001:db8:85a3::")]
    [InlineData("::ffff:198.51.100.9", "198.51.100.0")]
    public async Task Umami_receives_only_the_shortened_address_of_the_visitor(string visitor, string expected)
    {
        // Arrange
        using var umami = new RecordingHttpMessageHandler();
        await using var site = await PageViewTestApp.Start(umami, UmamiConnections.Default, visitorAddress: IPAddress.Parse(visitor));

        // Act
        using var page = await site.Visit(HttpMethod.Get, "/pt");
        var arrived = await umami.WaitForRequests(1, TestContext.Current.CancellationToken);
        var sent = arrived.ShouldHaveSingleItem();

        // Assert
        page.StatusCode.ShouldBe(HttpStatusCode.OK);
        sent.Message.Headers.NonValidated["X-Forwarded-For"].ToString().ShouldBe(expected);
    }

    [Fact]
    public async Task Requests_that_are_not_page_views_send_nothing()
    {
        // Arrange
        using var umami = new RecordingHttpMessageHandler();
        await using var site = await PageViewTestApp.Start(umami, UmamiConnections.Default);

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
        await using var site = await PageViewTestApp.Start(umami, UmamiConnections.Default);

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
        warnings.ShouldAllSatisfy(warning => warning.Level.ShouldBe(LogLevel.Warning));
        warnings.ShouldAllSatisfy(warning => warning.Exception.ShouldBeNull());
        warnings.ShouldAllSatisfy(warning => warning.Message.ShouldNotContain(TransportFailureMessage, StringComparison.Ordinal));
        warnings.ShouldAllSatisfy(warning => warning.Message.ShouldContain("umami", StringComparison.Ordinal));
        site.Logs.Records.ShouldAllSatisfy(record => record.ShouldHoldNoVisitorData());
        var dropped = site.Meters.Measurements.Where(measurement => measurement.Instrument == WebAnalyticsTelemetry.DroppedPageViewsCounterName).ToArray();
        dropped.ShouldAllSatisfy(measurement => measurement.Tags[WebAnalyticsTelemetry.TargetTag].ShouldBe("umami"));
        dropped.Select(measurement => (string?)measurement.Tags[WebAnalyticsTelemetry.ReasonTag])
            .Order(StringComparer.Ordinal)
            .ToArray()
            .ShouldBe([WebAnalyticsTelemetry.RejectedReason, WebAnalyticsTelemetry.SendFailedReason]);
    }

    [Fact]
    public async Task Every_target_receives_each_page_view_and_a_failing_target_stops_none_of_the_others()
    {
        // Arrange
        using var umami = new RecordingHttpMessageHandler();
        var broken = new FailingPageViewTarget("broken");
        var second = new RecordingPageViewTarget("second");
        await using var site = await PageViewTestApp.Start(
            umami,
            UmamiConnections.Default,
            addTargets: pageViews => pageViews.AddTarget(_ => broken).AddTarget(_ => second));

        // Act
        using var first = await site.Visit(HttpMethod.Get, VisitorDataAssertions.SecretPath);
        using var again = await site.Visit(HttpMethod.Get, "/en");
        var sentToUmami = await umami.WaitForRequests(2, TestContext.Current.CancellationToken);
        var sentToSecond = await second.WaitFor(2, TestContext.Current.CancellationToken);
        await site.Stop();

        // Assert
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        sentToUmami.Select(request => UmamiBodies.UrlOf(request.Body)).ToArray().ShouldBe([VisitorDataAssertions.SecretPath, "/en"]);
        sentToSecond.Select(pageView => pageView.PathAndQuery).ToArray().ShouldBe([VisitorDataAssertions.SecretPath, "/en"]);
        sentToSecond.ShouldAllSatisfy(pageView =>
        {
            pageView.UserAgent.ShouldBe(PageContexts.VisitorAgent);
            pageView.ClientIp.ShouldBe(PageContexts.ShortenedVisitorAddress);
            pageView.Hostname.ShouldBe("localhost");
        });
        broken.Calls.ShouldBe(2);
        var dropped = site.Meters.Measurements.Where(measurement => measurement.Instrument == WebAnalyticsTelemetry.DroppedPageViewsCounterName).ToArray();
        dropped.Length.ShouldBe(2);
        dropped.ShouldAllSatisfy(measurement =>
        {
            measurement.Tags[WebAnalyticsTelemetry.TargetTag].ShouldBe("broken");
            measurement.Tags[WebAnalyticsTelemetry.ReasonTag].ShouldBe(WebAnalyticsTelemetry.SendFailedReason);
        });
        var failures = site.Logs.Records.Where(record => record.EventId.Id == WebAnalyticsTelemetry.SendFailedEventId).ToArray();
        failures.Length.ShouldBe(2);
        failures.ShouldAllSatisfy(failure =>
        {
            failure.Message.ShouldContain("broken", StringComparison.Ordinal);
            failure.Message.ShouldContain(nameof(InvalidOperationException), StringComparison.Ordinal);
            failure.Message.ShouldNotContain(FailingPageViewTarget.FailureMessage, StringComparison.Ordinal);
            failure.Exception.ShouldBeNull();
        });
        site.Logs.Records.ShouldAllSatisfy(record => record.ShouldHoldNoVisitorData());
    }
}
