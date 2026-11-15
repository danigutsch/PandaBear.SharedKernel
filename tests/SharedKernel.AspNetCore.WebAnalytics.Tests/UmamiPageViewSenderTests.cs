using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using PandaBear.Testing.Http;
using PandaBear.Testing.Logging;
using PandaBear.Testing.Metrics;

namespace PandaBear.AspNetCore.WebAnalytics.Tests;

/// <summary>Tests the bounded queue and the background sender: a full queue drops and counts, stopping sends what is queued, and a stop that takes too long gives up and counts what it left.</summary>
public sealed class UmamiPageViewSenderTests
{
    [Fact]
    public void A_full_queue_drops_the_page_view_and_counts_it()
    {
        // Arrange
        using var umami = new RecordingHttpMessageHandler();
        using var meters = new MeterCollector(WebAnalyticsTelemetry.MeterName);
        using var services = SenderTestServices.Build(umami, meters, options => options.QueueCapacity = 1);
        var queue = services.GetRequiredService<UmamiPageViewQueue>();

        // Act
        var first = queue.TryEnqueue(SenderTestServices.PageView("/pt"));
        var second = queue.TryEnqueue(SenderTestServices.PageView("/en"));

        // Assert
        first.ShouldBeTrue();
        second.ShouldBeFalse();
        var dropped = meters.Measurements.ShouldHaveSingleItem();
        dropped.Instrument.ShouldBe(WebAnalyticsTelemetry.DroppedPageViewsCounterName);
        dropped.Value.ShouldBe(1);
        dropped.Tags[WebAnalyticsTelemetry.ReasonTag].ShouldBe(WebAnalyticsTelemetry.QueueFullReason);
    }

    [Fact]
    public void A_failing_meter_listener_does_not_break_dropping()
    {
        // Arrange
        using var umami = new RecordingHttpMessageHandler();
        using var meters = new MeterCollector(WebAnalyticsTelemetry.MeterName) { ThrowOnMeasurement = true };
        using var services = SenderTestServices.Build(umami, meters, options => options.QueueCapacity = 1);
        var queue = services.GetRequiredService<UmamiPageViewQueue>();
        queue.TryEnqueue(SenderTestServices.PageView("/pt"));

        // Act
        var enqueued = queue.TryEnqueue(SenderTestServices.PageView("/en"));

        // Assert
        enqueued.ShouldBeFalse();
    }

    [Fact]
    public async Task Stop_sends_every_queued_page_view_before_it_returns()
    {
        // Arrange
        using var umami = new RecordingHttpMessageHandler();
        using var meters = new MeterCollector(WebAnalyticsTelemetry.MeterName);
        using var services = SenderTestServices.Build(umami, meters);
        var queue = services.GetRequiredService<UmamiPageViewQueue>();
        var sender = SenderTestServices.Sender(services);
        await sender.StartAsync(TestContext.Current.CancellationToken);
        queue.TryEnqueue(SenderTestServices.PageView("/one"));
        queue.TryEnqueue(SenderTestServices.PageView("/two"));
        queue.TryEnqueue(SenderTestServices.PageView("/three"));

        // Act
        await sender.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var sentUrls = umami.Requests.Select(request => UmamiBodies.UrlOf(request.Body)).ToArray();
        sentUrls.ShouldBe(["/one", "/two", "/three"]);
        meters.Measurements.ShouldBeEmpty();
    }

    [Fact]
    public async Task Stop_gives_up_after_the_shutdown_timeout_and_counts_what_it_left()
    {
        // Arrange
        using var umami = new GatedHttpMessageHandler();
        using var meters = new MeterCollector(WebAnalyticsTelemetry.MeterName);
        using var logs = new RecordingLoggerProvider();
        var time = new FakeTimeProvider();
        using var services = SenderTestServices.Build(umami, meters, options => options.ShutdownTimeout = TimeSpan.FromSeconds(2), logs, time);
        var queue = services.GetRequiredService<UmamiPageViewQueue>();
        var sender = SenderTestServices.Sender(services);
        await sender.StartAsync(TestContext.Current.CancellationToken);
        queue.TryEnqueue(SenderTestServices.PageView("/one"));
        queue.TryEnqueue(SenderTestServices.PageView("/two"));
        queue.TryEnqueue(SenderTestServices.PageView("/three"));
        await umami.WaitForRequests(1, TestContext.Current.CancellationToken);

        // Act
        var stop = sender.StopAsync(TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(2));
        await stop;

        // Assert
        umami.Requests.Count.ShouldBe(1);
        var dropped = meters.Measurements.ShouldHaveSingleItem();
        dropped.Value.ShouldBe(3);
        dropped.Tags[WebAnalyticsTelemetry.ReasonTag].ShouldBe(WebAnalyticsTelemetry.ShutdownReason);
        var warning = logs.Records.ShouldHaveSingleItem(record => record.EventId.Id == WebAnalyticsTelemetry.UnsentAtShutdownEventId);
        warning.Level.ShouldBe(LogLevel.Warning);
        warning.Message.ShouldStartWith("3 page views", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_page_view_that_arrives_after_stop_is_dropped_and_counted_as_shutdown()
    {
        // Arrange
        using var umami = new RecordingHttpMessageHandler();
        using var meters = new MeterCollector(WebAnalyticsTelemetry.MeterName);
        using var services = SenderTestServices.Build(umami, meters);
        var queue = services.GetRequiredService<UmamiPageViewQueue>();
        var sender = SenderTestServices.Sender(services);
        await sender.StartAsync(TestContext.Current.CancellationToken);
        await sender.StopAsync(TestContext.Current.CancellationToken);

        // Act
        var enqueued = queue.TryEnqueue(SenderTestServices.PageView("/late"));

        // Assert
        enqueued.ShouldBeFalse();
        meters.Measurements.ShouldHaveSingleItem().Tags[WebAnalyticsTelemetry.ReasonTag].ShouldBe(WebAnalyticsTelemetry.ShutdownReason);
        umami.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Stop_without_start_returns_and_drops_nothing()
    {
        // Arrange
        using var umami = new RecordingHttpMessageHandler();
        using var meters = new MeterCollector(WebAnalyticsTelemetry.MeterName);
        using var services = SenderTestServices.Build(umami, meters);
        var sender = SenderTestServices.Sender(services);

        // Act
        await sender.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        meters.Measurements.ShouldBeEmpty();
    }
}
