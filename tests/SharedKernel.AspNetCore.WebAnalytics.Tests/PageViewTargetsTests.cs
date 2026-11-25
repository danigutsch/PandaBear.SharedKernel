using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharedKernel.Testing.Http;

namespace SharedKernel.AspNetCore.WebAnalytics.Tests;

/// <summary>
/// Tests that a target exists only when its own setting is present, like the OTLP exporter of the service defaults: without a target
/// nothing is captured, queued or sent, and with one each page view reaches it.
/// </summary>
public sealed class PageViewTargetsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Without_an_umami_connection_string_no_target_runs_and_nothing_is_captured_queued_or_sent(string? connectionString)
    {
        // Arrange
        using var umami = new RecordingHttpMessageHandler();
        await using var site = await PageViewTestApp.Start(umami, connectionString);

        // Act
        using var page = await site.Visit(HttpMethod.Get, "/pt/viagens");
        await site.Stop();

        // Assert
        page.StatusCode.ShouldBe(HttpStatusCode.OK);
        umami.Requests.ShouldBeEmpty();
        var registered = site.Services.GetRequiredService<IServiceProviderIsService>();
        registered.IsService(typeof(PageViewRecorder)).ShouldBeFalse("nothing captures a page view without a target");
        registered.IsService(typeof(PageViewQueues)).ShouldBeFalse("no queue exists without a target");
        site.Services.GetServices<IHostedService>().OfType<PageViewDispatcher>().ShouldBeEmpty("no background work runs without a target");
        var off = site.Logs.Records.ShouldHaveSingleItem(record => record.EventId.Id == WebAnalyticsTelemetry.PageViewsOffEventId);
        off.Level.ShouldBe(LogLevel.Information);
        site.Meters.Measurements.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_target_added_by_type_receives_the_page_views_without_umami()
    {
        // Arrange
        using var umami = new RecordingHttpMessageHandler();
        var typed = new RecordingPageViewTarget("typed");
        await using var site = await PageViewTestApp.Start(
            umami,
            connectionString: null,
            addTargets: pageViews =>
            {
                pageViews.Services.AddSingleton(typed);
                pageViews.AddTarget<RecordingPageViewTarget>();
            });

        // Act
        using var page = await site.Visit(HttpMethod.Get, "/pt/viagens?utm_source=newsletter&email=visitor%40example.com");
        var received = await typed.WaitFor(1, TestContext.Current.CancellationToken);

        // Assert
        page.StatusCode.ShouldBe(HttpStatusCode.OK);
        received.ShouldHaveSingleItem().PathAndQuery.ShouldBe("/pt/viagens?utm_source=newsletter");
        umami.Requests.ShouldBeEmpty();
        site.Logs.Records.ShouldNotContain(record => record.EventId.Id == WebAnalyticsTelemetry.PageViewsOffEventId);
    }
}
