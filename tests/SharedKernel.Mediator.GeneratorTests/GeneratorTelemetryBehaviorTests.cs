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
        (response).ShouldBe("tour:5");
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
        await ((Func<Task>)(() => mediator.Send((IRequest<string>)request, CancellationToken.None).AsTask())).ShouldThrowAssignableTo<InvalidOperationException>();

        // Assert
        var span = (stopped).ShouldHaveSingleItem(a => a.OperationName == MediatorTelemetry.ActivitySend);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        (span.Status).ShouldBe(ActivityStatusCode.Error);
        (span.StatusDescription).ShouldBeNull();
        (outcome).ShouldBe(MediatorTelemetry.OutcomeError);
        (errorType).ShouldBe("InvalidOperationException");

        (span.Events).ShouldNotContain(static evt => evt.Name == "exception");
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
        (response).ShouldBe("tour:7");
        var span = (stopped).ShouldHaveSingleItem(a => a.OperationName == MediatorTelemetry.ActivitySend);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        (span.Status).ShouldBe(ActivityStatusCode.Ok);
        (span.StatusDescription).ShouldBeNull();
        (outcome).ShouldBe(MediatorTelemetry.OutcomeSuccess);
        (errorType).ShouldBeNull();
        (span.Events).ShouldNotContain(static evt => evt.Name == "exception");
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
        (response).ShouldBe("fallback");
        var span = (stopped).ShouldHaveSingleItem(a => a.OperationName == MediatorTelemetry.ActivitySend);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        (span.Status).ShouldBe(ActivityStatusCode.Ok);
        (outcome).ShouldBe(MediatorTelemetry.OutcomeSuccess);
        (errorType).ShouldBeNull();
        (span.Events).ShouldNotContain(static evt => evt.Name == "exception");
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
        await ((Func<Task>)(() => mediator.Send((IRequest<string>)request, cts.Token).AsTask())).ShouldThrowAssignableTo<OperationCanceledException>();

        // Assert
        var span = (stopped).ShouldHaveSingleItem(a => a.OperationName == MediatorTelemetry.ActivitySend);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        (span.Status).ShouldBe(ActivityStatusCode.Unset);
        (span.StatusDescription).ShouldBeNull();
        (outcome).ShouldBe(MediatorTelemetry.OutcomeCancelled);
        (errorType).ShouldBeNull();
        (span.Events).ShouldNotContain(static evt => evt.Name == "exception");
    }

    [Fact]
    public async Task Records_unsignaled_send_cancellation_as_an_error()
    {
        // Arrange
        var stopped = new List<Activity>();
        using var root = new Activity("test-root");
        root.Start();
        using var listener = GeneratorTelemetryBehaviorTestsHelpers.CreateCapturingListener(stopped, root.TraceId);
        using var ctx = GeneratedMediatorRuntimeContext.Create(GeneratorDispatchBehaviorTestSources.SendWithUnexpectedCancellation());
        var mediator = ctx.CreateMediator(ctx.CreateInstance("Demo.GetTourHandler"));
        var request = ctx.CreateInstance("Demo.GetTour", 1);

        // Act
        await ((Func<Task>)(() => mediator.Send((IRequest<string>)request, CancellationToken.None).AsTask())).ShouldThrowAssignableTo<OperationCanceledException>();

        // Assert
        var span = stopped.ShouldHaveSingleItem(a => a.OperationName == MediatorTelemetry.ActivitySend);
        span.Status.ShouldBe(ActivityStatusCode.Error);
        span.StatusDescription.ShouldBeNull();
        span.GetTagItem(MediatorTelemetry.TagOutcome).ShouldBe(MediatorTelemetry.OutcomeError);
        span.GetTagItem(MediatorTelemetry.TagErrorType).ShouldBe(nameof(OperationCanceledException));
        span.Events.ShouldNotContain(static evt => evt.Name == "exception");
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
        await ((Func<Task>)(() => mediator.Publish((INotification)notification, cts.Token).AsTask())).ShouldThrowAssignableTo<OperationCanceledException>();

        // Assert
        var span = (stopped).ShouldHaveSingleItem(a => a.OperationName == MediatorTelemetry.ActivityPublish);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        (span.Status).ShouldBe(ActivityStatusCode.Unset);
        (span.StatusDescription).ShouldBeNull();
        (outcome).ShouldBe(MediatorTelemetry.OutcomeCancelled);
        (errorType).ShouldBeNull();
        (span.Events).ShouldNotContain(static evt => evt.Name == "exception");

    }

    [Fact]
    public async Task Records_unsignaled_publish_cancellation_as_an_error()
    {
        // Arrange
        var stopped = new List<Activity>();
        using var root = new Activity("test-root");
        root.Start();
        using var listener = GeneratorTelemetryBehaviorTestsHelpers.CreateCapturingListener(stopped, root.TraceId);
        using var ctx = GeneratedMediatorRuntimeContext.Create(GeneratorDispatchBehaviorTestSources.PublishWithUnexpectedCancellation());
        var mediator = ctx.CreateMediator(ctx.CreateInstance("Demo.TourCreatedHandler"));
        var notification = ctx.CreateInstance("Demo.TourCreated", 1);

        // Act
        await ((Func<Task>)(() => mediator.Publish((INotification)notification, CancellationToken.None).AsTask())).ShouldThrowAssignableTo<OperationCanceledException>();

        // Assert
        var span = stopped.ShouldHaveSingleItem(a => a.OperationName == MediatorTelemetry.ActivityPublish);
        span.Status.ShouldBe(ActivityStatusCode.Error);
        span.StatusDescription.ShouldBeNull();
        span.GetTagItem(MediatorTelemetry.TagOutcome).ShouldBe(MediatorTelemetry.OutcomeError);
        span.GetTagItem(MediatorTelemetry.TagErrorType).ShouldBe(nameof(OperationCanceledException));
        span.Events.ShouldNotContain(static evt => evt.Name == "exception");
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
        var span = (stopped).ShouldHaveSingleItem(a => a.OperationName == MediatorTelemetry.ActivityPublish);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        (span.Status).ShouldBe(ActivityStatusCode.Ok);
        (span.StatusDescription).ShouldBeNull();
        (outcome).ShouldBe(MediatorTelemetry.OutcomeSuccess);
        (errorType).ShouldBeNull();
        (span.Events).ShouldNotContain(static evt => evt.Name == "exception");

    }

    [Fact]
    public async Task Records_publish_error_type_without_exception_content()
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
        await ((Func<Task>)(() => mediator.Publish((INotification)notification, CancellationToken.None).AsTask())).ShouldThrowAssignableTo<InvalidOperationException>();

        // Assert
        var span = (stopped).ShouldHaveSingleItem(a => a.OperationName == MediatorTelemetry.ActivityPublish);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        (span.Status).ShouldBe(ActivityStatusCode.Error);
        (span.StatusDescription).ShouldBeNull();
        (outcome).ShouldBe(MediatorTelemetry.OutcomeError);
        (errorType).ShouldBe("InvalidOperationException");

        (span.Events).ShouldNotContain(static evt => evt.Name == "exception");

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
        (handlerSpans.Length).ShouldBe(2);
        (handlerSpans).ShouldAllSatisfy(static handlerSpan =>
        {
            var handlerOutcome = handlerSpan.GetTagItem(MediatorTelemetry.TagOutcome);
            var handlerErrorType = handlerSpan.GetTagItem(MediatorTelemetry.TagErrorType);
            (handlerSpan.Status).ShouldBe(ActivityStatusCode.Ok);
            (handlerSpan.StatusDescription).ShouldBeNull();
            (handlerOutcome).ShouldBe(MediatorTelemetry.OutcomeSuccess);
            (handlerErrorType).ShouldBeNull();
            (handlerSpan.Events).ShouldNotContain(static evt => evt.Name == "exception");
        });
        (handlerSpans).ShouldContain(static span => string.Equals(span.GetTagItem(MediatorTelemetry.TagHandlerName) as string, "TourCreatedHandlerOne", StringComparison.Ordinal));
        (handlerSpans).ShouldContain(static span => string.Equals(span.GetTagItem(MediatorTelemetry.TagHandlerName) as string, "TourCreatedHandlerTwo", StringComparison.Ordinal));
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
        await ((Func<Task>)(() => mediator.Publish((INotification)notification, cts.Token).AsTask())).ShouldThrowAssignableTo<OperationCanceledException>();

        // Assert
        var handlerSpan = (stopped).ShouldHaveSingleItem(a => a.OperationName == MediatorTelemetry.ActivityNotificationHandler);
        var handlerOutcome = handlerSpan.GetTagItem(MediatorTelemetry.TagOutcome);
        var handlerErrorType = handlerSpan.GetTagItem(MediatorTelemetry.TagErrorType);
        var handlerName = handlerSpan.GetTagItem(MediatorTelemetry.TagHandlerName);
        (handlerSpan.Status).ShouldBe(ActivityStatusCode.Unset);
        (handlerSpan.StatusDescription).ShouldBeNull();
        (handlerOutcome).ShouldBe(MediatorTelemetry.OutcomeCancelled);
        (handlerErrorType).ShouldBeNull();
        (handlerName).ShouldBe("TourCreatedHandlerOne");
        (handlerSpan.Events).ShouldNotContain(static evt => evt.Name == "exception");
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
        await ((Func<Task>)(() => mediator.Publish((INotification)notification, CancellationToken.None).AsTask())).ShouldThrowAssignableTo<InvalidOperationException>();

        // Assert
        var handlerSpan = (stopped).ShouldHaveSingleItem(a => a.OperationName == MediatorTelemetry.ActivityNotificationHandler);
        var handlerOutcome = handlerSpan.GetTagItem(MediatorTelemetry.TagOutcome);
        var handlerErrorType = handlerSpan.GetTagItem(MediatorTelemetry.TagErrorType);
        var handlerName = handlerSpan.GetTagItem(MediatorTelemetry.TagHandlerName);
        (handlerSpan.Status).ShouldBe(ActivityStatusCode.Error);
        (handlerSpan.StatusDescription).ShouldBeNull();
        (handlerOutcome).ShouldBe(MediatorTelemetry.OutcomeError);
        (handlerErrorType).ShouldBe("InvalidOperationException");
        (handlerName).ShouldBe("TourCreatedHandlerOne");

        (handlerSpan.Events).ShouldNotContain(static evt => evt.Name == "exception");
    }

    [Fact]
    public async Task Captures_stream_error_type_without_exception_content()
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
        await ((Func<Task>)(async () =>
        {
            await foreach (var _ in mediator.Send((IStreamRequest<string>)request, CancellationToken.None))
            {
                // consume until exception
            }
        })).ShouldThrowAssignableTo<InvalidOperationException>();

        // Assert
        var span = (stopped).ShouldHaveSingleItem(a => a.OperationName == MediatorTelemetry.ActivityStream);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        (span.Status).ShouldBe(ActivityStatusCode.Error);
        (span.StatusDescription).ShouldBeNull();
        (outcome).ShouldBe(MediatorTelemetry.OutcomeError);
        (errorType).ShouldBe("InvalidOperationException");

        (span.Events).ShouldNotContain(static evt => evt.Name == "exception");
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
        await ((Func<Task>)(async () =>
        {
            await foreach (var _ in mediator.Send((IStreamRequest<string>)request, cts.Token))
            {
                await cts.CancelAsync();
            }
        })).ShouldThrowAssignableTo<OperationCanceledException>();

        // Assert
        var span = (stopped).ShouldHaveSingleItem(a => a.OperationName == MediatorTelemetry.ActivityStream);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        (span.Status).ShouldBe(ActivityStatusCode.Unset);
        (span.StatusDescription).ShouldBeNull();
        (outcome).ShouldBe(MediatorTelemetry.OutcomeCancelled);
        (errorType).ShouldBeNull();
        (span.Events).ShouldNotContain(static evt => evt.Name == "exception");
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
        (stopped).ShouldNotContain(a => a.OperationName == MediatorTelemetry.ActivityStream);

        await foreach (var _ in stream)
        {
            // consume
        }

        // Assert — span closed after full enumeration
        var span = (stopped).ShouldHaveSingleItem(a => a.OperationName == MediatorTelemetry.ActivityStream);
        var outcome = span.GetTagItem(MediatorTelemetry.TagOutcome);
        var errorType = span.GetTagItem(MediatorTelemetry.TagErrorType);
        (span.StatusDescription).ShouldBeNull();
        (outcome).ShouldBe(MediatorTelemetry.OutcomeSuccess);
        (errorType).ShouldBeNull();
        (span.Events).ShouldNotContain(static evt => evt.Name == "exception");
    }

}
