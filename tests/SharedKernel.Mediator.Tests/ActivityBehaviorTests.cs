using System.Diagnostics;

namespace SharedKernel.Mediator.Tests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ContractsCapability)]
public sealed class ActivityBehaviorTests
{
    [Fact]
    public void SharedKernel_mediator_activity_source_uses_stable_package_name()
    {
        // Arrange
        var expectedName = MediatorTelemetry.Name;

        // Act
        var activitySourceName = SharedKernelMediatorActivitySource.ActivitySourceName;
        var sourceName = SharedKernelMediatorActivitySource.Source.Name;

        // Assert
        TestAssert.Equal(expectedName, activitySourceName);
        TestAssert.Equal(expectedName, sourceName);
    }

    [Fact]
    public void Mediator_telemetry_contract_uses_stable_names()
    {
        // Assert
        TestAssert.Equal("SharedKernel.Mediator", MediatorTelemetry.Name);
        TestAssert.Equal("mediator.send", MediatorTelemetry.ActivitySend);
        TestAssert.Equal("mediator.stream", MediatorTelemetry.ActivityStream);
        TestAssert.Equal("mediator.publish", MediatorTelemetry.ActivityPublish);
        TestAssert.Equal("mediator.notification.handle", MediatorTelemetry.ActivityNotificationHandler);
        TestAssert.Equal("mediator.requests", MediatorTelemetry.MetricRequests);
        TestAssert.Equal("mediator.request.duration", MediatorTelemetry.MetricRequestDuration);
        TestAssert.Equal("mediator.notifications", MediatorTelemetry.MetricNotifications);
        TestAssert.Equal("mediator.notification.duration", MediatorTelemetry.MetricNotificationDuration);
        TestAssert.Equal("mediator.streams", MediatorTelemetry.MetricStreams);
        TestAssert.Equal("sharedkernel.mediator.request_type", MediatorTelemetry.TagRequestType);
        TestAssert.Equal("sharedkernel.mediator.response_type", MediatorTelemetry.TagResponseType);
        TestAssert.Equal("sharedkernel.mediator.outcome", MediatorTelemetry.TagRuntimeOutcome);
        TestAssert.Equal("mediator.request.name", MediatorTelemetry.TagRequestName);
        TestAssert.Equal("mediator.request.assembly", MediatorTelemetry.TagRequestAssembly);
        TestAssert.Equal("mediator.handler.name", MediatorTelemetry.TagHandlerName);
        TestAssert.Equal("mediator.pipeline.depth", MediatorTelemetry.TagPipelineDepth);
        TestAssert.Equal("mediator.notification.name", MediatorTelemetry.TagNotificationName);
        TestAssert.Equal("mediator.notification.assembly", MediatorTelemetry.TagNotificationAssembly);
        TestAssert.Equal("mediator.notification.handler.count", MediatorTelemetry.TagNotificationHandlerCount);
        TestAssert.Equal("mediator.outcome", MediatorTelemetry.TagOutcome);
        TestAssert.Equal("error.type", MediatorTelemetry.TagErrorType);
        TestAssert.Equal("success", MediatorTelemetry.OutcomeSuccess);
        TestAssert.Equal("cancelled", MediatorTelemetry.OutcomeCancelled);
        TestAssert.Equal("error", MediatorTelemetry.OutcomeError);
    }

    [Fact]
    public async Task Activity_behavior_starts_request_activity_when_a_listener_is_registered()
    {
        // Arrange
        List<Activity> stoppedActivities = [];
        using var listener = ActivityBehaviorTestsHelpers.CreateCapturingListener(stoppedActivities);
        var behavior = new ActivityBehavior<ActivityTestQuery, int>();
        var request = new ActivityTestQuery(42);

        // Act
        var response = await behavior.Handle(request, () => ValueTask.FromResult(request.Id + 1), CancellationToken.None);

        // Assert
        TestAssert.Equal(43, response);
        var activity = TestAssert.ExactlyOne(stoppedActivities);
        TestAssert.Equal(nameof(ActivityTestQuery), activity.OperationName);
        TestAssert.Equal(SharedKernelMediatorActivitySource.ActivitySourceName, activity.Source.Name);
        TestAssert.Equal(ActivityStatusCode.Ok, activity.Status);
        TestAssert.Contains(activity.Tags, static tag => tag.Key == MediatorTelemetry.TagRequestType && tag.Value == typeof(ActivityTestQuery).FullName);
        TestAssert.Contains(activity.Tags, static tag => tag.Key == MediatorTelemetry.TagResponseType && tag.Value == typeof(int).FullName);
        TestAssert.Contains(activity.Tags, static tag => tag.Key == MediatorTelemetry.TagRuntimeOutcome && tag.Value == MediatorTelemetry.OutcomeSuccess);
        TestAssert.DoesNotContain(activity.Tags, static tag => tag.Key == MediatorTelemetry.TagErrorType);
        TestAssert.DoesNotContain(activity.Events, static evt => evt.Name == "exception");
    }

    [Fact]
    public async Task Activity_behavior_completes_when_no_listener_is_registered()
    {
        // Arrange
        var behavior = new ActivityBehavior<ActivityTestQuery, int>();
        var request = new ActivityTestQuery(7);

        // Act
        var response = await behavior.Handle(request, () => ValueTask.FromResult(request.Id * 2), CancellationToken.None);

        // Assert
        TestAssert.Equal(14, response);
    }

    [Fact]
    public async Task Activity_behavior_records_exception_event_when_the_handler_fails()
    {
        // Arrange
        List<Activity> stoppedActivities = [];
        using var listener = ActivityBehaviorTestsHelpers.CreateCapturingListener(stoppedActivities);
        var behavior = new ActivityBehavior<ActivityTestQuery, int>();
        var request = new ActivityTestQuery(11);

        // Act
        await TestAssert.Throws<InvalidOperationException>(() =>
            behavior.Handle(
                request,
                static () => ValueTask.FromException<int>(new InvalidOperationException("boom")),
                CancellationToken.None).AsTask());

        // Assert
        var activity = TestAssert.ExactlyOne(stoppedActivities);
        TestAssert.Equal(ActivityStatusCode.Error, activity.Status);
        TestAssert.Equal("boom", activity.StatusDescription);
        TestAssert.Contains(activity.Tags, static tag => tag.Key == MediatorTelemetry.TagErrorType && tag.Value == "InvalidOperationException");
        TestAssert.Contains(activity.Tags, static tag => tag.Key == MediatorTelemetry.TagRuntimeOutcome && tag.Value == MediatorTelemetry.OutcomeError);

        var exceptionEvent = TestAssert.ExactlyOne(activity.Events, static evt => evt.Name == "exception");
        var exceptionTags = exceptionEvent.Tags;
        _ = TestAssert.NotNull(exceptionTags);
        TestAssert.Contains(exceptionTags, static tag =>
            tag.Key == "exception.type" && string.Equals(tag.Value as string, typeof(InvalidOperationException).FullName, StringComparison.Ordinal));
        TestAssert.Contains(exceptionTags, static tag =>
            tag.Key == "exception.message" && string.Equals(tag.Value as string, "boom", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Activity_behavior_does_not_record_exception_event_when_the_handler_is_cancelled()
    {
        // Arrange
        List<Activity> stoppedActivities = [];
        using var listener = ActivityBehaviorTestsHelpers.CreateCapturingListener(stoppedActivities);
        var behavior = new ActivityBehavior<ActivityTestQuery, int>();
        var request = new ActivityTestQuery(12);

        // Act
        await TestAssert.Throws<OperationCanceledException>(() =>
            behavior.Handle(
                request,
                static () => ValueTask.FromException<int>(new OperationCanceledException("cancelled")),
                CancellationToken.None).AsTask());

        // Assert
        var activity = TestAssert.ExactlyOne(stoppedActivities);
        TestAssert.Equal(ActivityStatusCode.Unset, activity.Status);
        TestAssert.Null(activity.StatusDescription);
        TestAssert.Contains(activity.Tags, static tag => tag.Key == MediatorTelemetry.TagRuntimeOutcome && tag.Value == MediatorTelemetry.OutcomeCancelled);
        TestAssert.DoesNotContain(activity.Tags, static tag => tag.Key == MediatorTelemetry.TagErrorType);
        TestAssert.DoesNotContain(activity.Events, static evt => evt.Name == "exception");
    }

    [Fact]
    public async Task Activity_behavior_does_not_record_an_error_when_the_handler_handles_the_exception_internally()
    {
        // Arrange
        List<Activity> stoppedActivities = [];
        using var listener = ActivityBehaviorTestsHelpers.CreateCapturingListener(stoppedActivities);
        var behavior = new ActivityBehavior<ActivityTestQuery, int>();
        var request = new ActivityTestQuery(13);

        // Act
        var response = await behavior.Handle(
            request,
            ActivityBehaviorTestsHelpers.HandleExceptionInternally,
            CancellationToken.None);

        // Assert
        TestAssert.Equal(99, response);
        var activity = TestAssert.ExactlyOne(stoppedActivities);
        TestAssert.Equal(ActivityStatusCode.Ok, activity.Status);
        TestAssert.Null(activity.StatusDescription);
        TestAssert.Contains(activity.Tags, static tag => tag.Key == MediatorTelemetry.TagRuntimeOutcome && tag.Value == MediatorTelemetry.OutcomeSuccess);
        TestAssert.DoesNotContain(activity.Tags, static tag => tag.Key == MediatorTelemetry.TagErrorType);
        TestAssert.DoesNotContain(activity.Events, static evt => evt.Name == "exception");
    }

}
