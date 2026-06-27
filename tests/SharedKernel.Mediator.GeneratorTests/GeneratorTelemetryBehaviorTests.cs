using System.Diagnostics;

namespace SharedKernel.Mediator.GeneratorTests;

public sealed class GeneratorTelemetryBehaviorTests
{

    [Fact]
    public async Task Completes_send_safely_when_no_listener_is_registered()
    {
        // Arrange
        using var ctx = GeneratedMediatorRuntimeContext.Create(GeneratorDispatchBehaviorTestSources.SendSuccess());
        var mediator = ctx.CreateMediator(ctx.CreateInstance("Demo.GetTourHandler"));
        var request = ctx.CreateInstance("Demo.GetTour", 5);

        // Act
        var response = await mediator.Send((IRequest<string>)request, CancellationToken.None);

        // Assert
        Assert.Equal("tour:5", response);
    }

    [Fact]
    public async Task Records_error_outcome_and_error_type_tag_for_a_send_exception()
    {
        // Arrange
        var stopped = new List<Activity>();
        using var root = new Activity("test-root");
        root.Start();
        using var listener = GeneratorTelemetryBehaviorTestsHelpers.CreateCapturingListener(stopped, root.TraceId);
        using var ctx = GeneratedMediatorRuntimeContext.Create(GeneratorDispatchBehaviorTestSources.SendWithException());
        var mediator = ctx.CreateMediator(ctx.CreateInstance("Demo.GetTourHandler"));
        var request = ctx.CreateInstance("Demo.GetTour", 1);

        // Act
        await Assert.ThrowsAnyAsync<InvalidOperationException>(
            () => mediator.Send((IRequest<string>)request, CancellationToken.None).AsTask());

        // Assert
        var span = Assert.Single(stopped, a => a.OperationName == MediatorTelemetry.ActivitySend);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("handler boom", span.StatusDescription);
        Assert.Equal(MediatorTelemetry.OutcomeError, outcome);
        Assert.Equal("InvalidOperationException", errorType);

        var exceptionEvent = Assert.Single(span.Events, static evt => evt.Name == "exception");
        var exceptionTags = exceptionEvent.Tags;
        Assert.NotNull(exceptionTags);
        Assert.Contains(exceptionTags, static tag =>
            tag.Key == "exception.type" && string.Equals(tag.Value as string, typeof(InvalidOperationException).FullName, StringComparison.Ordinal));
        Assert.Contains(exceptionTags, static tag =>
            tag.Key == "exception.message" && string.Equals(tag.Value as string, "handler boom", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Does_not_record_an_error_for_a_successful_send()
    {
        // Arrange
        var stopped = new List<Activity>();
        using var root = new Activity("test-root");
        root.Start();
        using var listener = GeneratorTelemetryBehaviorTestsHelpers.CreateCapturingListener(stopped, root.TraceId);
        using var ctx = GeneratedMediatorRuntimeContext.Create(GeneratorDispatchBehaviorTestSources.SendSuccess());
        var mediator = ctx.CreateMediator(ctx.CreateInstance("Demo.GetTourHandler"));
        var request = ctx.CreateInstance("Demo.GetTour", 7);

        // Act
        var response = await mediator.Send((IRequest<string>)request, CancellationToken.None);

        // Assert
        Assert.Equal("tour:7", response);
        var span = Assert.Single(stopped, a => a.OperationName == MediatorTelemetry.ActivitySend);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        Assert.Equal(ActivityStatusCode.Ok, span.Status);
        Assert.Null(span.StatusDescription);
        Assert.Equal(MediatorTelemetry.OutcomeSuccess, outcome);
        Assert.Null(errorType);
        Assert.DoesNotContain(span.Events, static evt => evt.Name == "exception");
    }

    [Fact]
    public async Task Does_not_record_an_error_when_a_send_handler_handles_the_exception_internally()
    {
        // Arrange
        var stopped = new List<Activity>();
        using var root = new Activity("test-root");
        root.Start();
        using var listener = GeneratorTelemetryBehaviorTestsHelpers.CreateCapturingListener(stopped, root.TraceId);
        using var ctx = GeneratedMediatorRuntimeContext.Create(GeneratorDispatchBehaviorTestSources.SendWithHandledException());
        var mediator = ctx.CreateMediator(ctx.CreateInstance("Demo.GetTourHandler"));
        var request = ctx.CreateInstance("Demo.GetTour", 1);

        // Act
        var response = await mediator.Send((IRequest<string>)request, CancellationToken.None);

        // Assert
        Assert.Equal("fallback", response);
        var span = Assert.Single(stopped, a => a.OperationName == MediatorTelemetry.ActivitySend);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        Assert.Equal(ActivityStatusCode.Ok, span.Status);
        Assert.Equal(MediatorTelemetry.OutcomeSuccess, outcome);
        Assert.Null(errorType);
        Assert.DoesNotContain(span.Events, static evt => evt.Name == "exception");
    }

    [Fact]
    public async Task Records_a_cancelled_outcome_for_send_cancellation()
    {
        // Arrange
        var stopped = new List<Activity>();
        using var root = new Activity("test-root");
        root.Start();
        using var listener = GeneratorTelemetryBehaviorTestsHelpers.CreateCapturingListener(stopped, root.TraceId);
        using var ctx = GeneratedMediatorRuntimeContext.Create(GeneratorDispatchBehaviorTestSources.SendWithCancellation());
        var mediator = ctx.CreateMediator(ctx.CreateInstance("Demo.GetTourHandler"));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var request = ctx.CreateInstance("Demo.GetTour", 1);

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => mediator.Send((IRequest<string>)request, cts.Token).AsTask());

        // Assert
        var span = Assert.Single(stopped, a => a.OperationName == MediatorTelemetry.ActivitySend);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        Assert.Equal(ActivityStatusCode.Unset, span.Status);
        Assert.Null(span.StatusDescription);
        Assert.Equal(MediatorTelemetry.OutcomeCancelled, outcome);
        Assert.Null(errorType);
        Assert.DoesNotContain(span.Events, static evt => evt.Name == "exception");
    }

    [Fact]
    public async Task Records_a_cancelled_outcome_for_publish_cancellation()
    {
        // Arrange
        var stopped = new List<Activity>();
        using var root = new Activity("test-root");
        root.Start();
        using var listener = GeneratorTelemetryBehaviorTestsHelpers.CreateCapturingListener(stopped, root.TraceId);
        using var ctx = GeneratedMediatorRuntimeContext.Create(GeneratorDispatchBehaviorTestSources.PublishWithCancellation());
        var mediator = ctx.CreateMediator(ctx.CreateInstance("Demo.TourCreatedHandler"));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var notification = ctx.CreateInstance("Demo.TourCreated", 1);

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => mediator.Publish((INotification)notification, cts.Token).AsTask());

        // Assert
        var span = Assert.Single(stopped, a => a.OperationName == MediatorTelemetry.ActivityPublish);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        Assert.Equal(ActivityStatusCode.Unset, span.Status);
        Assert.Null(span.StatusDescription);
        Assert.Equal(MediatorTelemetry.OutcomeCancelled, outcome);
        Assert.Null(errorType);
        Assert.DoesNotContain(span.Events, static evt => evt.Name == "exception");

    }

    [Fact]
    public async Task Does_not_record_an_error_for_a_successful_publish()
    {
        // Arrange
        var stopped = new List<Activity>();
        using var root = new Activity("test-root");
        root.Start();
        using var listener = GeneratorTelemetryBehaviorTestsHelpers.CreateCapturingListener(stopped, root.TraceId);
        using var ctx = GeneratedMediatorRuntimeContext.Create(GeneratorDispatchBehaviorTestSources.PublishSuccess());
        var mediator = ctx.CreateMediator(ctx.CreateInstance("Demo.TourCreatedHandler"));
        var notification = ctx.CreateInstance("Demo.TourCreated", 1);

        // Act
        await mediator.Publish((INotification)notification, CancellationToken.None);

        // Assert
        var span = Assert.Single(stopped, a => a.OperationName == MediatorTelemetry.ActivityPublish);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        Assert.Equal(ActivityStatusCode.Ok, span.Status);
        Assert.Null(span.StatusDescription);
        Assert.Equal(MediatorTelemetry.OutcomeSuccess, outcome);
        Assert.Null(errorType);
        Assert.DoesNotContain(span.Events, static evt => evt.Name == "exception");

    }

    [Fact]
    public async Task Records_a_single_exception_event_for_a_publish_exception()
    {
        // Arrange
        var stopped = new List<Activity>();
        using var root = new Activity("test-root");
        root.Start();
        using var listener = GeneratorTelemetryBehaviorTestsHelpers.CreateCapturingListener(stopped, root.TraceId);
        using var ctx = GeneratedMediatorRuntimeContext.Create(GeneratorDispatchBehaviorTestSources.PublishWithException());
        var mediator = ctx.CreateMediator(ctx.CreateInstance("Demo.TourCreatedHandler"));
        var notification = ctx.CreateInstance("Demo.TourCreated", 1);

        // Act
        await Assert.ThrowsAnyAsync<InvalidOperationException>(
            () => mediator.Publish((INotification)notification, CancellationToken.None).AsTask());

        // Assert
        var span = Assert.Single(stopped, a => a.OperationName == MediatorTelemetry.ActivityPublish);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("handler boom", span.StatusDescription);
        Assert.Equal(MediatorTelemetry.OutcomeError, outcome);
        Assert.Equal("InvalidOperationException", errorType);

        var exceptionEvent = Assert.Single(span.Events, static evt => evt.Name == "exception");
        var exceptionTags = exceptionEvent.Tags;
        Assert.NotNull(exceptionTags);
        Assert.Contains(exceptionTags, static tag =>
            tag.Key == "exception.type" && string.Equals(tag.Value as string, typeof(InvalidOperationException).FullName, StringComparison.Ordinal));
        Assert.Contains(exceptionTags, static tag =>
            tag.Key == "exception.message" && string.Equals(tag.Value as string, "handler boom", StringComparison.Ordinal));

    }

    [Fact]
    public async Task Records_a_successful_notification_handler_span_for_sequential_publish()
    {
        // Arrange
        var stopped = new List<Activity>();
        using var root = new Activity("test-root");
        root.Start();
        using var listener = GeneratorTelemetryBehaviorTestsHelpers.CreateCapturingListener(stopped, root.TraceId);
        using var ctx = GeneratedMediatorRuntimeContext.Create(GeneratorDispatchBehaviorTestSources.SequentialPublishSuccess());
        var mediator = ctx.CreateMediator(
            ctx.CreateInstance("Demo.TourCreatedHandlerOne"),
            ctx.CreateInstance("Demo.TourCreatedHandlerTwo"));
        var notification = ctx.CreateInstance("Demo.TourCreated", 1);

        // Act
        await mediator.Publish((INotification)notification, CancellationToken.None);

        // Assert
        var handlerSpans = stopped.Where(a => a.OperationName == MediatorTelemetry.ActivityNotificationHandler).ToArray();
        Assert.Equal(2, handlerSpans.Length);
        Assert.All(handlerSpans, static handlerSpan =>
        {
            var handlerOutcome = handlerSpan.GetTagItem(MediatorTelemetry.TagOutcome);
            var handlerErrorType = handlerSpan.GetTagItem(MediatorTelemetry.TagErrorType);
            Assert.Equal(ActivityStatusCode.Ok, handlerSpan.Status);
            Assert.Null(handlerSpan.StatusDescription);
            Assert.Equal(MediatorTelemetry.OutcomeSuccess, handlerOutcome);
            Assert.Null(handlerErrorType);
            Assert.DoesNotContain(handlerSpan.Events, static evt => evt.Name == "exception");
        });
        Assert.Contains(handlerSpans, static span => string.Equals(span.GetTagItem(MediatorTelemetry.TagHandlerName) as string, "TourCreatedHandlerOne", StringComparison.Ordinal));
        Assert.Contains(handlerSpans, static span => string.Equals(span.GetTagItem(MediatorTelemetry.TagHandlerName) as string, "TourCreatedHandlerTwo", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Records_a_cancelled_notification_handler_span_for_sequential_publish()
    {
        // Arrange
        var stopped = new List<Activity>();
        using var root = new Activity("test-root");
        root.Start();
        using var listener = GeneratorTelemetryBehaviorTestsHelpers.CreateCapturingListener(stopped, root.TraceId);
        using var ctx = GeneratedMediatorRuntimeContext.Create(GeneratorDispatchBehaviorTestSources.SequentialPublishWithCancellation());
        var mediator = ctx.CreateMediator(
            ctx.CreateInstance("Demo.TourCreatedHandlerOne"),
            ctx.CreateInstance("Demo.TourCreatedHandlerTwo"));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var notification = ctx.CreateInstance("Demo.TourCreated", 1);

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => mediator.Publish((INotification)notification, cts.Token).AsTask());

        // Assert
        var handlerSpan = Assert.Single(stopped, a => a.OperationName == MediatorTelemetry.ActivityNotificationHandler);
        var handlerOutcome = handlerSpan.GetTagItem(MediatorTelemetry.TagOutcome);
        var handlerErrorType = handlerSpan.GetTagItem(MediatorTelemetry.TagErrorType);
        var handlerName = handlerSpan.GetTagItem(MediatorTelemetry.TagHandlerName);
        Assert.Equal(ActivityStatusCode.Unset, handlerSpan.Status);
        Assert.Null(handlerSpan.StatusDescription);
        Assert.Equal(MediatorTelemetry.OutcomeCancelled, handlerOutcome);
        Assert.Null(handlerErrorType);
        Assert.Equal("TourCreatedHandlerOne", handlerName);
        Assert.DoesNotContain(handlerSpan.Events, static evt => evt.Name == "exception");
    }

    [Fact]
    public async Task Records_a_failed_notification_handler_span_for_sequential_publish()
    {
        // Arrange
        var stopped = new List<Activity>();
        using var root = new Activity("test-root");
        root.Start();
        using var listener = GeneratorTelemetryBehaviorTestsHelpers.CreateCapturingListener(stopped, root.TraceId);
        using var ctx = GeneratedMediatorRuntimeContext.Create(GeneratorDispatchBehaviorTestSources.SequentialPublishWithException());
        var mediator = ctx.CreateMediator(
            ctx.CreateInstance("Demo.TourCreatedHandlerOne"),
            ctx.CreateInstance("Demo.TourCreatedHandlerTwo"));
        var notification = ctx.CreateInstance("Demo.TourCreated", 1);

        // Act
        await Assert.ThrowsAnyAsync<InvalidOperationException>(
            () => mediator.Publish((INotification)notification, CancellationToken.None).AsTask());

        // Assert
        var handlerSpan = Assert.Single(stopped, a => a.OperationName == MediatorTelemetry.ActivityNotificationHandler);
        var handlerOutcome = handlerSpan.GetTagItem(MediatorTelemetry.TagOutcome);
        var handlerErrorType = handlerSpan.GetTagItem(MediatorTelemetry.TagErrorType);
        var handlerName = handlerSpan.GetTagItem(MediatorTelemetry.TagHandlerName);
        Assert.Equal(ActivityStatusCode.Error, handlerSpan.Status);
        Assert.Equal("handler boom", handlerSpan.StatusDescription);
        Assert.Equal(MediatorTelemetry.OutcomeError, handlerOutcome);
        Assert.Equal("InvalidOperationException", handlerErrorType);
        Assert.Equal("TourCreatedHandlerOne", handlerName);

        var handlerExceptionEvent = Assert.Single(handlerSpan.Events, static evt => evt.Name == "exception");
        var handlerExceptionTags = handlerExceptionEvent.Tags;
        Assert.NotNull(handlerExceptionTags);
        Assert.Contains(handlerExceptionTags, static tag =>
            tag.Key == "exception.type" && string.Equals(tag.Value as string, typeof(InvalidOperationException).FullName, StringComparison.Ordinal));
        Assert.Contains(handlerExceptionTags, static tag =>
            tag.Key == "exception.message" && string.Equals(tag.Value as string, "handler boom", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Captures_a_stream_enumeration_exception_on_the_span()
    {
        // Arrange
        var stopped = new List<Activity>();
        using var root = new Activity("test-root");
        root.Start();
        using var listener = GeneratorTelemetryBehaviorTestsHelpers.CreateCapturingListener(stopped, root.TraceId);
        using var ctx = GeneratedMediatorRuntimeContext.Create(GeneratorDispatchBehaviorTestSources.StreamDispatchWithExceptionNoPipelines());
        var mediator = ctx.CreateMediator(ctx.CreateInstance("Demo.StreamToursHandler"));
        var request = ctx.CreateInstance("Demo.StreamTours", 2);

        // Act
        await Assert.ThrowsAnyAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in mediator.Send((IStreamRequest<string>)request, CancellationToken.None))
            {
                // consume until exception
            }
        });

        // Assert
        var span = Assert.Single(stopped, a => a.OperationName == MediatorTelemetry.ActivityStream);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("boom", span.StatusDescription);
        Assert.Equal(MediatorTelemetry.OutcomeError, outcome);
        Assert.Equal("InvalidOperationException", errorType);

        var exceptionEvent = Assert.Single(span.Events, static evt => evt.Name == "exception");
        var exceptionTags = exceptionEvent.Tags;
        Assert.NotNull(exceptionTags);
        Assert.Contains(exceptionTags, static tag =>
            tag.Key == "exception.type" && string.Equals(tag.Value as string, typeof(InvalidOperationException).FullName, StringComparison.Ordinal));
        Assert.Contains(exceptionTags, static tag =>
            tag.Key == "exception.message" && string.Equals(tag.Value as string, "boom", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Records_a_cancelled_outcome_for_stream_cancellation_during_enumeration()
    {
        // Arrange
        var stopped = new List<Activity>();
        using var root = new Activity("test-root");
        root.Start();
        using var listener = GeneratorTelemetryBehaviorTestsHelpers.CreateCapturingListener(stopped, root.TraceId);
        using var ctx = GeneratedMediatorRuntimeContext.Create(GeneratorDispatchBehaviorTestSources.StreamDispatchWithCancellationNoPipelines());
        var mediator = ctx.CreateMediator(ctx.CreateInstance("Demo.StreamToursHandler"));
        using var cts = new CancellationTokenSource();
        var request = ctx.CreateInstance("Demo.StreamTours", 10);

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in mediator.Send((IStreamRequest<string>)request, cts.Token))
            {
                await cts.CancelAsync();
            }
        });

        // Assert
        var span = Assert.Single(stopped, a => a.OperationName == MediatorTelemetry.ActivityStream);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        Assert.Equal(ActivityStatusCode.Unset, span.Status);
        Assert.Null(span.StatusDescription);
        Assert.Equal(MediatorTelemetry.OutcomeCancelled, outcome);
        Assert.Null(errorType);
        Assert.DoesNotContain(span.Events, static evt => evt.Name == "exception");
    }

    [Fact]
    public async Task Closes_the_stream_span_after_enumeration_instead_of_after_send()
    {
        // Arrange
        var stopped = new List<Activity>();
        using var root = new Activity("test-root");
        root.Start();
        using var listener = GeneratorTelemetryBehaviorTestsHelpers.CreateCapturingListener(stopped, root.TraceId);
        using var ctx = GeneratedMediatorRuntimeContext.Create(GeneratorDispatchBehaviorTestSources.StreamDispatchNoPipelines());
        var mediator = ctx.CreateMediator(ctx.CreateInstance("Demo.StreamToursHandler"));
        var request = ctx.CreateInstance("Demo.StreamTours", 3);

        // Act
        var stream = mediator.Send((IStreamRequest<string>)request, CancellationToken.None);

        // The span must NOT be stopped yet — Send() just returns IAsyncEnumerable
        Assert.DoesNotContain(stopped, a => a.OperationName == MediatorTelemetry.ActivityStream);

        await foreach (var _ in stream)
        {
            // consume
        }

        // Assert — span closed after full enumeration
        var span = Assert.Single(stopped, a => a.OperationName == MediatorTelemetry.ActivityStream);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        Assert.Null(span.StatusDescription);
        Assert.Equal(MediatorTelemetry.OutcomeSuccess, outcome);
        Assert.Null(errorType);
        Assert.DoesNotContain(span.Events, static evt => evt.Name == "exception");
    }

}
