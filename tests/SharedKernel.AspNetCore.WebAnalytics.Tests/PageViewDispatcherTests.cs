using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using PandaBear.Testing.Http;
using PandaBear.Testing.Logging;
using PandaBear.Testing.Metrics;

namespace PandaBear.AspNetCore.WebAnalytics.Tests;

/// <summary>
/// Tests the bounded queue of each target and the background dispatcher: a full queue drops and counts for its own target only, stopping
/// sends what is queued, and a stop that takes too long gives up and counts what it left.
/// </summary>
public sealed class PageViewDispatcherTests
{
    [Fact]
    public void A_full_queue_drops_the_page_view_and_counts_it_for_its_target()
    {
        // Arrange
        using var umami = new RecordingHttpMessageHandler();
        using var meters = new MeterCollector(WebAnalyticsTelemetry.MeterName);
        using var host = SenderTestServices.Build(umami, meters, options => options.QueueCapacity = 1);
        var queues = SenderTestServices.Queues(host);

        // Act
        queues.Enqueue(SenderTestServices.PageView("/pt"));
        queues.Enqueue(SenderTestServices.PageView("/en"));

        // Assert
        var dropped = meters.Measurements.ShouldHaveSingleItem();
        dropped.Instrument.ShouldBe(WebAnalyticsTelemetry.DroppedPageViewsCounterName);
        dropped.Value.ShouldBe(1);
        dropped.Tags[WebAnalyticsTelemetry.TargetTag].ShouldBe("umami");
        dropped.Tags[WebAnalyticsTelemetry.ReasonTag].ShouldBe(WebAnalyticsTelemetry.QueueFullReason);
    }

    [Fact]
    public async Task A_slow_target_drops_only_its_own_page_views_and_the_others_receive_every_one()
    {
        // Arrange
        using var umami = new GatedHttpMessageHandler();
        using var meters = new MeterCollector(WebAnalyticsTelemetry.MeterName);
        var second = new RecordingPageViewTarget("second");
        using var host = SenderTestServices.Build(umami, meters, options => options.QueueCapacity = 1, addTargets: pageViews => pageViews.AddTarget(_ => second));
        var queues = SenderTestServices.Queues(host);
        var dispatcher = SenderTestServices.Dispatcher(host);
        await dispatcher.StartAsync(TestContext.Current.CancellationToken);
        queues.Enqueue(SenderTestServices.PageView("/one"));
        await umami.WaitForRequests(1, TestContext.Current.CancellationToken);
        await second.WaitFor(1, TestContext.Current.CancellationToken);
        queues.Enqueue(SenderTestServices.PageView("/two"));
        await second.WaitFor(1, TestContext.Current.CancellationToken);

        // Act
        queues.Enqueue(SenderTestServices.PageView("/three"));
        var third = await second.WaitFor(1, TestContext.Current.CancellationToken);

        // Assert
        third.ShouldHaveSingleItem().PathAndQuery.ShouldBe("/three");
        var dropped = meters.Measurements.ShouldHaveSingleItem();
        dropped.Tags[WebAnalyticsTelemetry.TargetTag].ShouldBe("umami");
        dropped.Tags[WebAnalyticsTelemetry.ReasonTag].ShouldBe(WebAnalyticsTelemetry.QueueFullReason);
        umami.Release();
        await dispatcher.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public void A_failing_meter_listener_does_not_break_dropping()
    {
        // Arrange
        using var umami = new RecordingHttpMessageHandler();
        using var meters = new MeterCollector(WebAnalyticsTelemetry.MeterName) { ThrowOnMeasurement = true };
        using var host = SenderTestServices.Build(umami, meters, options => options.QueueCapacity = 1);
        var queues = SenderTestServices.Queues(host);
        queues.Enqueue(SenderTestServices.PageView("/pt"));

        // Act
        queues.Enqueue(SenderTestServices.PageView("/en"));

        // Assert
        queues.All.ShouldHaveSingleItem().Reader.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Stop_sends_every_queued_page_view_before_it_returns()
    {
        // Arrange
        using var umami = new RecordingHttpMessageHandler();
        using var meters = new MeterCollector(WebAnalyticsTelemetry.MeterName);
        using var host = SenderTestServices.Build(umami, meters);
        var queues = SenderTestServices.Queues(host);
        var dispatcher = SenderTestServices.Dispatcher(host);
        await dispatcher.StartAsync(TestContext.Current.CancellationToken);
        queues.Enqueue(SenderTestServices.PageView("/one"));
        queues.Enqueue(SenderTestServices.PageView("/two"));
        queues.Enqueue(SenderTestServices.PageView("/three"));

        // Act
        await dispatcher.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var sentUrls = umami.Requests.Select(request => UmamiBodies.UrlOf(request.Body)).ToArray();
        sentUrls.ShouldBe(["/one", "/two", "/three"]);
        meters.Measurements.ShouldBeEmpty();
    }

    [Fact]
    public async Task Stop_gives_up_after_the_shutdown_timeout_and_counts_what_it_left_for_its_target()
    {
        // Arrange
        using var umami = new GatedHttpMessageHandler();
        using var meters = new MeterCollector(WebAnalyticsTelemetry.MeterName);
        using var logs = new RecordingLoggerProvider();
        var time = new FakeTimeProvider();
        using var host = SenderTestServices.Build(umami, meters, options => options.ShutdownTimeout = TimeSpan.FromSeconds(2), logs, time);
        var queues = SenderTestServices.Queues(host);
        var dispatcher = SenderTestServices.Dispatcher(host);
        await dispatcher.StartAsync(TestContext.Current.CancellationToken);
        queues.Enqueue(SenderTestServices.PageView("/one"));
        queues.Enqueue(SenderTestServices.PageView("/two"));
        queues.Enqueue(SenderTestServices.PageView("/three"));
        await umami.WaitForRequests(1, TestContext.Current.CancellationToken);

        // Act
        var stop = dispatcher.StopAsync(TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(2));
        await stop;

        // Assert
        umami.Requests.Count.ShouldBe(1);
        var dropped = meters.Measurements.ShouldHaveSingleItem();
        dropped.Value.ShouldBe(3);
        dropped.Tags[WebAnalyticsTelemetry.TargetTag].ShouldBe("umami");
        dropped.Tags[WebAnalyticsTelemetry.ReasonTag].ShouldBe(WebAnalyticsTelemetry.ShutdownReason);
        var warning = logs.Records.ShouldHaveSingleItem(record => record.EventId.Id == WebAnalyticsTelemetry.UnsentAtShutdownEventId);
        warning.Level.ShouldBe(LogLevel.Warning);
        warning.Message.ShouldStartWith("3 page views", StringComparison.Ordinal);
        warning.Message.ShouldContain("umami", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_page_view_that_arrives_after_stop_is_dropped_and_counted_as_shutdown()
    {
        // Arrange
        using var umami = new RecordingHttpMessageHandler();
        using var meters = new MeterCollector(WebAnalyticsTelemetry.MeterName);
        using var host = SenderTestServices.Build(umami, meters);
        var queues = SenderTestServices.Queues(host);
        var dispatcher = SenderTestServices.Dispatcher(host);
        await dispatcher.StartAsync(TestContext.Current.CancellationToken);
        await dispatcher.StopAsync(TestContext.Current.CancellationToken);

        // Act
        queues.Enqueue(SenderTestServices.PageView("/late"));

        // Assert
        var dropped = meters.Measurements.ShouldHaveSingleItem();
        dropped.Tags[WebAnalyticsTelemetry.TargetTag].ShouldBe("umami");
        dropped.Tags[WebAnalyticsTelemetry.ReasonTag].ShouldBe(WebAnalyticsTelemetry.ShutdownReason);
        umami.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Stop_without_start_returns_and_drops_nothing()
    {
        // Arrange
        using var umami = new RecordingHttpMessageHandler();
        using var meters = new MeterCollector(WebAnalyticsTelemetry.MeterName);
        using var host = SenderTestServices.Build(umami, meters);
        var dispatcher = SenderTestServices.Dispatcher(host);

        // Act
        await dispatcher.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        meters.Measurements.ShouldBeEmpty();
    }
}
