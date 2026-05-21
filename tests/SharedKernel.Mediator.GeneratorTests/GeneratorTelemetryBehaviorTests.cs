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
    public async Task Records_Error_Outcome_And_Error_Type_Tag_For_A_Send_Exception()
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
        var outcome = span.GetTagItem("mediator.outcome");
        var errorType = span.GetTagItem("error.type");
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("error", outcome);
        Assert.Equal("InvalidOperationException", errorType);

        var exceptionEvent = Assert.Single(span.Events, static evt => evt.Name == "exception");
        Assert.Contains(exceptionEvent.Tags!, static tag =>
            tag.Key == "exception.type" && string.Equals(tag.Value as string, typeof(InvalidOperationException).FullName, StringComparison.Ordinal));
        Assert.Contains(exceptionEvent.Tags!, static tag =>
            tag.Key == "exception.message" && string.Equals(tag.Value as string, "handler boom", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Records_A_Cancelled_Outcome_For_Publish_Cancellation()
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
        var outcome = span.GetTagItem("mediator.outcome");
        var errorType = span.GetTagItem("error.type");
        Assert.Equal("cancelled", outcome);
        Assert.Null(errorType);
        Assert.DoesNotContain(span.Events, static evt => evt.Name == "exception");
    }

    [Fact]
    public async Task Captures_A_Stream_Enumeration_Exception_On_The_Span()
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
        var outcome = span.GetTagItem("mediator.outcome");
        var errorType = span.GetTagItem("error.type");
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("error", outcome);
        Assert.Equal("InvalidOperationException", errorType);

        var exceptionEvent = Assert.Single(span.Events, static evt => evt.Name == "exception");
        Assert.Contains(exceptionEvent.Tags!, static tag =>
            tag.Key == "exception.type" && string.Equals(tag.Value as string, typeof(InvalidOperationException).FullName, StringComparison.Ordinal));
        Assert.Contains(exceptionEvent.Tags!, static tag =>
            tag.Key == "exception.message" && string.Equals(tag.Value as string, "boom", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Records_A_Cancelled_Outcome_For_Stream_Cancellation_During_Enumeration()
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
        var outcome = span.GetTagItem("mediator.outcome");
        var errorType = span.GetTagItem("error.type");
        Assert.Equal("cancelled", outcome);
        Assert.Null(errorType);
        Assert.DoesNotContain(span.Events, static evt => evt.Name == "exception");
    }

    [Fact]
    public async Task Closes_The_Stream_Span_After_Enumeration_Instead_Of_After_Send()
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
        var outcome = span.GetTagItem("mediator.outcome");
        Assert.Equal("success", outcome);
    }
}
