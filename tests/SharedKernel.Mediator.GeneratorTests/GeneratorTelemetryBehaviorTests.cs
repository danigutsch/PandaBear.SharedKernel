using System.Diagnostics;

namespace SharedKernel.Mediator.GeneratorTests;

public sealed class GeneratorTelemetryBehaviorTests
{
    private static ActivityListener CreateCapturingListener(List<Activity> stopped, ActivityTraceId traceId)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == SharedKernelMediatorActivitySource.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a =>
            {
                if (a.TraceId == traceId)
                {
                    stopped.Add(a);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    [Fact]
    public async Task Send_exception_records_error_outcome_with_error_type_tag()
    {
        // Arrange
        var stopped = new List<Activity>();
        using var root = new Activity("test-root");
        root.Start();
        using var listener = CreateCapturingListener(stopped, root.TraceId);
        using var ctx = GeneratedMediatorRuntimeContext.Create(GeneratorDispatchBehaviorTestSources.SendWithException());
        var mediator = ctx.CreateMediator(ctx.CreateInstance("Demo.GetTourHandler"));
        var request = ctx.CreateInstance("Demo.GetTour", 1);

        // Act
        await Assert.ThrowsAnyAsync<InvalidOperationException>(
            () => mediator.Send<string>((IRequest<string>)request, CancellationToken.None).AsTask());

        // Assert
        var span = Assert.Single(stopped, a => a.OperationName == "mediator.send");
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("error", span.GetTagItem("mediator.outcome"));
        Assert.Equal("InvalidOperationException", span.GetTagItem("error.type"));
    }

    [Fact]
    public async Task Publish_cancellation_records_cancelled_outcome()
    {
        // Arrange
        var stopped = new List<Activity>();
        using var root = new Activity("test-root");
        root.Start();
        using var listener = CreateCapturingListener(stopped, root.TraceId);
        using var ctx = GeneratedMediatorRuntimeContext.Create(GeneratorDispatchBehaviorTestSources.PublishWithCancellation());
        var mediator = ctx.CreateMediator(ctx.CreateInstance("Demo.TourCreatedHandler"));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var notification = ctx.CreateInstance("Demo.TourCreated", 1);

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => mediator.Publish((INotification)notification, cts.Token).AsTask());

        // Assert
        var span = Assert.Single(stopped, a => a.OperationName == "mediator.publish");
        Assert.Equal("cancelled", span.GetTagItem("mediator.outcome"));
        Assert.Null(span.GetTagItem("error.type"));
    }

    [Fact]
    public async Task Stream_exception_during_enumeration_is_captured_on_span()
    {
        // Arrange
        var stopped = new List<Activity>();
        using var root = new Activity("test-root");
        root.Start();
        using var listener = CreateCapturingListener(stopped, root.TraceId);
        using var ctx = GeneratedMediatorRuntimeContext.Create(GeneratorDispatchBehaviorTestSources.StreamDispatchWithExceptionNoPipelines());
        var mediator = ctx.CreateMediator(ctx.CreateInstance("Demo.StreamToursHandler"));
        var request = ctx.CreateInstance("Demo.StreamTours", 2);

        // Act
        await Assert.ThrowsAnyAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in mediator.Send<string>((IStreamRequest<string>)request, CancellationToken.None))
            {
                // consume until exception
            }
        });

        // Assert
        var span = Assert.Single(stopped, a => a.OperationName == "mediator.stream");
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("error", span.GetTagItem("mediator.outcome"));
        Assert.Equal("InvalidOperationException", span.GetTagItem("error.type"));
    }

    [Fact]
    public async Task Stream_cancellation_during_enumeration_records_cancelled_outcome()
    {
        // Arrange
        var stopped = new List<Activity>();
        using var root = new Activity("test-root");
        root.Start();
        using var listener = CreateCapturingListener(stopped, root.TraceId);
        using var ctx = GeneratedMediatorRuntimeContext.Create(GeneratorDispatchBehaviorTestSources.StreamDispatchWithCancellationNoPipelines());
        var mediator = ctx.CreateMediator(ctx.CreateInstance("Demo.StreamToursHandler"));
        using var cts = new CancellationTokenSource();
        var request = ctx.CreateInstance("Demo.StreamTours", 10);

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in mediator.Send<string>((IStreamRequest<string>)request, cts.Token))
            {
                await cts.CancelAsync();
            }
        });

        // Assert
        var span = Assert.Single(stopped, a => a.OperationName == "mediator.stream");
        Assert.Equal("cancelled", span.GetTagItem("mediator.outcome"));
        Assert.Null(span.GetTagItem("error.type"));
    }

    [Fact]
    public async Task Stream_span_is_closed_after_enumeration_not_after_send()
    {
        // Arrange
        var stopped = new List<Activity>();
        using var root = new Activity("test-root");
        root.Start();
        using var listener = CreateCapturingListener(stopped, root.TraceId);
        using var ctx = GeneratedMediatorRuntimeContext.Create(GeneratorDispatchBehaviorTestSources.StreamDispatchNoPipelines());
        var mediator = ctx.CreateMediator(ctx.CreateInstance("Demo.StreamToursHandler"));
        var request = ctx.CreateInstance("Demo.StreamTours", 3);

        // Act
        var stream = mediator.Send<string>((IStreamRequest<string>)request, CancellationToken.None);

        // The span must NOT be stopped yet — Send() just returns IAsyncEnumerable
        Assert.DoesNotContain(stopped, a => a.OperationName == "mediator.stream");

        await foreach (var _ in stream)
        {
            // consume
        }

        // Assert — span closed after full enumeration
        var span = Assert.Single(stopped, a => a.OperationName == "mediator.stream");
        Assert.Equal("success", span.GetTagItem("mediator.outcome"));
    }
}
