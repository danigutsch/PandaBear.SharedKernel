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
        TestAssert.Equal("tour:5", response);
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
        await TestAssert.ThrowsAny<InvalidOperationException>(
            () => mediator.Send((IRequest<string>)request, CancellationToken.None).AsTask());

        // Assert
        var span = TestAssert.ExactlyOne(stopped, a => a.OperationName == MediatorTelemetry.ActivitySend);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        TestAssert.Equal(ActivityStatusCode.Error, span.Status);
        TestAssert.Equal("handler boom", span.StatusDescription);
        TestAssert.Equal(MediatorTelemetry.OutcomeError, outcome);
        TestAssert.Equal("InvalidOperationException", errorType);

        var exceptionEvent = TestAssert.ExactlyOne(span.Events, static evt => evt.Name == "exception");
        var exceptionTags = exceptionEvent.Tags;
        _ = TestAssert.NotNull(exceptionTags);
        TestAssert.Contains(exceptionTags, static tag =>
            tag.Key == "exception.type" && string.Equals(tag.Value as string, typeof(InvalidOperationException).FullName, StringComparison.Ordinal));
        TestAssert.Contains(exceptionTags, static tag =>
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
        TestAssert.Equal("tour:7", response);
        var span = TestAssert.ExactlyOne(stopped, a => a.OperationName == MediatorTelemetry.ActivitySend);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        TestAssert.Equal(ActivityStatusCode.Ok, span.Status);
        TestAssert.Null(span.StatusDescription);
        TestAssert.Equal(MediatorTelemetry.OutcomeSuccess, outcome);
        TestAssert.Null(errorType);
        TestAssert.DoesNotContain(span.Events, static evt => evt.Name == "exception");
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
        TestAssert.Equal("fallback", response);
        var span = TestAssert.ExactlyOne(stopped, a => a.OperationName == MediatorTelemetry.ActivitySend);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        TestAssert.Equal(ActivityStatusCode.Ok, span.Status);
        TestAssert.Equal(MediatorTelemetry.OutcomeSuccess, outcome);
        TestAssert.Null(errorType);
        TestAssert.DoesNotContain(span.Events, static evt => evt.Name == "exception");
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
        await TestAssert.ThrowsAny<OperationCanceledException>(
            () => mediator.Send((IRequest<string>)request, cts.Token).AsTask());

        // Assert
        var span = TestAssert.ExactlyOne(stopped, a => a.OperationName == MediatorTelemetry.ActivitySend);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        TestAssert.Equal(ActivityStatusCode.Unset, span.Status);
        TestAssert.Null(span.StatusDescription);
        TestAssert.Equal(MediatorTelemetry.OutcomeCancelled, outcome);
        TestAssert.Null(errorType);
        TestAssert.DoesNotContain(span.Events, static evt => evt.Name == "exception");
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
        await TestAssert.ThrowsAny<OperationCanceledException>(
            () => mediator.Publish((INotification)notification, cts.Token).AsTask());

        // Assert
        var span = TestAssert.ExactlyOne(stopped, a => a.OperationName == MediatorTelemetry.ActivityPublish);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        TestAssert.Equal(ActivityStatusCode.Unset, span.Status);
        TestAssert.Null(span.StatusDescription);
        TestAssert.Equal(MediatorTelemetry.OutcomeCancelled, outcome);
        TestAssert.Null(errorType);
        TestAssert.DoesNotContain(span.Events, static evt => evt.Name == "exception");

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
        var span = TestAssert.ExactlyOne(stopped, a => a.OperationName == MediatorTelemetry.ActivityPublish);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        TestAssert.Equal(ActivityStatusCode.Ok, span.Status);
        TestAssert.Null(span.StatusDescription);
        TestAssert.Equal(MediatorTelemetry.OutcomeSuccess, outcome);
        TestAssert.Null(errorType);
        TestAssert.DoesNotContain(span.Events, static evt => evt.Name == "exception");

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
        await TestAssert.ThrowsAny<InvalidOperationException>(
            () => mediator.Publish((INotification)notification, CancellationToken.None).AsTask());

        // Assert
        var span = TestAssert.ExactlyOne(stopped, a => a.OperationName == MediatorTelemetry.ActivityPublish);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        TestAssert.Equal(ActivityStatusCode.Error, span.Status);
        TestAssert.Equal("handler boom", span.StatusDescription);
        TestAssert.Equal(MediatorTelemetry.OutcomeError, outcome);
        TestAssert.Equal("InvalidOperationException", errorType);

        var exceptionEvent = TestAssert.ExactlyOne(span.Events, static evt => evt.Name == "exception");
        var exceptionTags = exceptionEvent.Tags;
        _ = TestAssert.NotNull(exceptionTags);
        TestAssert.Contains(exceptionTags, static tag =>
            tag.Key == "exception.type" && string.Equals(tag.Value as string, typeof(InvalidOperationException).FullName, StringComparison.Ordinal));
        TestAssert.Contains(exceptionTags, static tag =>
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
        TestAssert.Equal(2, handlerSpans.Length);
        TestAssert.All(handlerSpans, static handlerSpan =>
        {
            var handlerOutcome = handlerSpan.GetTagItem(MediatorTelemetry.TagOutcome);
            var handlerErrorType = handlerSpan.GetTagItem(MediatorTelemetry.TagErrorType);
            TestAssert.Equal(ActivityStatusCode.Ok, handlerSpan.Status);
            TestAssert.Null(handlerSpan.StatusDescription);
            TestAssert.Equal(MediatorTelemetry.OutcomeSuccess, handlerOutcome);
            TestAssert.Null(handlerErrorType);
            TestAssert.DoesNotContain(handlerSpan.Events, static evt => evt.Name == "exception");
        });
        TestAssert.Contains(handlerSpans, static span => string.Equals(span.GetTagItem(MediatorTelemetry.TagHandlerName) as string, "TourCreatedHandlerOne", StringComparison.Ordinal));
        TestAssert.Contains(handlerSpans, static span => string.Equals(span.GetTagItem(MediatorTelemetry.TagHandlerName) as string, "TourCreatedHandlerTwo", StringComparison.Ordinal));
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
        await TestAssert.ThrowsAny<OperationCanceledException>(
            () => mediator.Publish((INotification)notification, cts.Token).AsTask());

        // Assert
        var handlerSpan = TestAssert.ExactlyOne(stopped, a => a.OperationName == MediatorTelemetry.ActivityNotificationHandler);
        var handlerOutcome = handlerSpan.GetTagItem(MediatorTelemetry.TagOutcome);
        var handlerErrorType = handlerSpan.GetTagItem(MediatorTelemetry.TagErrorType);
        var handlerName = handlerSpan.GetTagItem(MediatorTelemetry.TagHandlerName);
        TestAssert.Equal(ActivityStatusCode.Unset, handlerSpan.Status);
        TestAssert.Null(handlerSpan.StatusDescription);
        TestAssert.Equal(MediatorTelemetry.OutcomeCancelled, handlerOutcome);
        TestAssert.Null(handlerErrorType);
        TestAssert.Equal("TourCreatedHandlerOne", handlerName);
        TestAssert.DoesNotContain(handlerSpan.Events, static evt => evt.Name == "exception");
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
        await TestAssert.ThrowsAny<InvalidOperationException>(
            () => mediator.Publish((INotification)notification, CancellationToken.None).AsTask());

        // Assert
        var handlerSpan = TestAssert.ExactlyOne(stopped, a => a.OperationName == MediatorTelemetry.ActivityNotificationHandler);
        var handlerOutcome = handlerSpan.GetTagItem(MediatorTelemetry.TagOutcome);
        var handlerErrorType = handlerSpan.GetTagItem(MediatorTelemetry.TagErrorType);
        var handlerName = handlerSpan.GetTagItem(MediatorTelemetry.TagHandlerName);
        TestAssert.Equal(ActivityStatusCode.Error, handlerSpan.Status);
        TestAssert.Equal("handler boom", handlerSpan.StatusDescription);
        TestAssert.Equal(MediatorTelemetry.OutcomeError, handlerOutcome);
        TestAssert.Equal("InvalidOperationException", handlerErrorType);
        TestAssert.Equal("TourCreatedHandlerOne", handlerName);

        var handlerExceptionEvent = TestAssert.ExactlyOne(handlerSpan.Events, static evt => evt.Name == "exception");
        var handlerExceptionTags = handlerExceptionEvent.Tags;
        _ = TestAssert.NotNull(handlerExceptionTags);
        TestAssert.Contains(handlerExceptionTags, static tag =>
            tag.Key == "exception.type" && string.Equals(tag.Value as string, typeof(InvalidOperationException).FullName, StringComparison.Ordinal));
        TestAssert.Contains(handlerExceptionTags, static tag =>
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
        await TestAssert.ThrowsAny<InvalidOperationException>(async () =>
        {
            await foreach (var _ in mediator.Send((IStreamRequest<string>)request, CancellationToken.None))
            {
                // consume until exception
            }
        });

        // Assert
        var span = TestAssert.ExactlyOne(stopped, a => a.OperationName == MediatorTelemetry.ActivityStream);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        TestAssert.Equal(ActivityStatusCode.Error, span.Status);
        TestAssert.Equal("boom", span.StatusDescription);
        TestAssert.Equal(MediatorTelemetry.OutcomeError, outcome);
        TestAssert.Equal("InvalidOperationException", errorType);

        var exceptionEvent = TestAssert.ExactlyOne(span.Events, static evt => evt.Name == "exception");
        var exceptionTags = exceptionEvent.Tags;
        _ = TestAssert.NotNull(exceptionTags);
        TestAssert.Contains(exceptionTags, static tag =>
            tag.Key == "exception.type" && string.Equals(tag.Value as string, typeof(InvalidOperationException).FullName, StringComparison.Ordinal));
        TestAssert.Contains(exceptionTags, static tag =>
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
        await TestAssert.ThrowsAny<OperationCanceledException>(async () =>
        {
            await foreach (var _ in mediator.Send((IStreamRequest<string>)request, cts.Token))
            {
                await cts.CancelAsync();
            }
        });

        // Assert
        var span = TestAssert.ExactlyOne(stopped, a => a.OperationName == MediatorTelemetry.ActivityStream);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        TestAssert.Equal(ActivityStatusCode.Unset, span.Status);
        TestAssert.Null(span.StatusDescription);
        TestAssert.Equal(MediatorTelemetry.OutcomeCancelled, outcome);
        TestAssert.Null(errorType);
        TestAssert.DoesNotContain(span.Events, static evt => evt.Name == "exception");
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
        TestAssert.DoesNotContain(stopped, a => a.OperationName == MediatorTelemetry.ActivityStream);

        await foreach (var _ in stream)
        {
            // consume
        }

        // Assert — span closed after full enumeration
        var span = TestAssert.ExactlyOne(stopped, a => a.OperationName == MediatorTelemetry.ActivityStream);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        TestAssert.Null(span.StatusDescription);
        TestAssert.Equal(MediatorTelemetry.OutcomeSuccess, outcome);
        TestAssert.Null(errorType);
        TestAssert.DoesNotContain(span.Events, static evt => evt.Name == "exception");
    }

}
