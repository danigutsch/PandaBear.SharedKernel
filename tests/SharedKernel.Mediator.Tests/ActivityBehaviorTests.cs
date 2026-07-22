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
        (activitySourceName).ShouldBe(expectedName);
        (sourceName).ShouldBe(expectedName);
    }

    [Fact]
    public void Mediator_telemetry_contract_uses_stable_names()
    {
        // Assert
        (MediatorTelemetry.Name).ShouldBe("SharedKernel.Mediator");
        (MediatorTelemetry.ActivitySend).ShouldBe("mediator.send");
        (MediatorTelemetry.ActivityStream).ShouldBe("mediator.stream");
        (MediatorTelemetry.ActivityPublish).ShouldBe("mediator.publish");
        (MediatorTelemetry.ActivityNotificationHandler).ShouldBe("mediator.notification.handle");
        (MediatorTelemetry.MetricRequests).ShouldBe("mediator.requests");
        (MediatorTelemetry.MetricRequestDuration).ShouldBe("mediator.request.duration");
        (MediatorTelemetry.MetricNotifications).ShouldBe("mediator.notifications");
        (MediatorTelemetry.MetricNotificationDuration).ShouldBe("mediator.notification.duration");
        (MediatorTelemetry.MetricStreams).ShouldBe("mediator.streams");
        (MediatorTelemetry.TagRequestType).ShouldBe("sharedkernel.mediator.request_type");
        (MediatorTelemetry.TagResponseType).ShouldBe("sharedkernel.mediator.response_type");
        (MediatorTelemetry.TagRuntimeOutcome).ShouldBe("sharedkernel.mediator.outcome");
        (MediatorTelemetry.TagRequestName).ShouldBe("mediator.request.name");
        (MediatorTelemetry.TagRequestAssembly).ShouldBe("mediator.request.assembly");
        (MediatorTelemetry.TagHandlerName).ShouldBe("mediator.handler.name");
        (MediatorTelemetry.TagPipelineDepth).ShouldBe("mediator.pipeline.depth");
        (MediatorTelemetry.TagNotificationName).ShouldBe("mediator.notification.name");
        (MediatorTelemetry.TagNotificationAssembly).ShouldBe("mediator.notification.assembly");
        (MediatorTelemetry.TagNotificationHandlerCount).ShouldBe("mediator.notification.handler.count");
        (MediatorTelemetry.TagOutcome).ShouldBe("mediator.outcome");
        (MediatorTelemetry.TagErrorType).ShouldBe("error.type");
        (MediatorTelemetry.OutcomeSuccess).ShouldBe("success");
        (MediatorTelemetry.OutcomeCancelled).ShouldBe("cancelled");
        (MediatorTelemetry.OutcomeError).ShouldBe("error");
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
        (response).ShouldBe(43);
        var activity = (stoppedActivities).ShouldHaveSingleItem();
        (activity.OperationName).ShouldBe(nameof(ActivityTestQuery));
        (activity.Source.Name).ShouldBe(SharedKernelMediatorActivitySource.ActivitySourceName);
        (activity.Status).ShouldBe(ActivityStatusCode.Ok);
        (activity.Tags).ShouldContain(static tag => tag.Key == MediatorTelemetry.TagRequestType && tag.Value == typeof(ActivityTestQuery).FullName);
        (activity.Tags).ShouldContain(static tag => tag.Key == MediatorTelemetry.TagResponseType && tag.Value == typeof(int).FullName);
        (activity.Tags).ShouldContain(static tag => tag.Key == MediatorTelemetry.TagRuntimeOutcome && tag.Value == MediatorTelemetry.OutcomeSuccess);
        (activity.Tags).ShouldNotContain(static tag => tag.Key == MediatorTelemetry.TagErrorType);
        (activity.Events).ShouldNotContain(static evt => evt.Name == "exception");
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
        (response).ShouldBe(14);
    }

    [Fact]
    public async Task Activity_behavior_records_bounded_error_type_without_exception_content_when_the_handler_fails()
    {
        // Arrange
        List<Activity> stoppedActivities = [];
        using var listener = ActivityBehaviorTestsHelpers.CreateCapturingListener(stoppedActivities);
        var behavior = new ActivityBehavior<ActivityTestQuery, int>();
        var request = new ActivityTestQuery(11);

        // Act
        await ((Func<Task>)(() =>
            behavior.Handle(
                request,
                static () => ValueTask.FromException<int>(new InvalidOperationException("boom")),
                CancellationToken.None).AsTask())).ShouldThrow<InvalidOperationException>();

        // Assert
        var activity = (stoppedActivities).ShouldHaveSingleItem();
        (activity.Status).ShouldBe(ActivityStatusCode.Error);
        (activity.StatusDescription).ShouldBeNull();
        (activity.Tags).ShouldContain(static tag => tag.Key == MediatorTelemetry.TagErrorType && tag.Value == "InvalidOperationException");
        (activity.Tags).ShouldContain(static tag => tag.Key == MediatorTelemetry.TagRuntimeOutcome && tag.Value == MediatorTelemetry.OutcomeError);

        (activity.Events).ShouldNotContain(static evt => evt.Name == "exception");
    }

    [Fact]
    public async Task Activity_behavior_does_not_record_exception_event_when_the_handler_is_cancelled()
    {
        // Arrange
        List<Activity> stoppedActivities = [];
        using var listener = ActivityBehaviorTestsHelpers.CreateCapturingListener(stoppedActivities);
        var behavior = new ActivityBehavior<ActivityTestQuery, int>();
        var request = new ActivityTestQuery(12);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        await ((Func<Task>)(() =>
            behavior.Handle(
                request,
                () => ValueTask.FromException<int>(new OperationCanceledException(cancellation.Token)),
                cancellation.Token).AsTask())).ShouldThrow<OperationCanceledException>();

        // Assert
        var activity = (stoppedActivities).ShouldHaveSingleItem();
        (activity.Status).ShouldBe(ActivityStatusCode.Unset);
        (activity.StatusDescription).ShouldBeNull();
        (activity.Tags).ShouldContain(static tag => tag.Key == MediatorTelemetry.TagRuntimeOutcome && tag.Value == MediatorTelemetry.OutcomeCancelled);
        (activity.Tags).ShouldNotContain(static tag => tag.Key == MediatorTelemetry.TagErrorType);
        (activity.Events).ShouldNotContain(static evt => evt.Name == "exception");
    }

    [Fact]
    public async Task Activity_behavior_records_unsignaled_cancellation_as_an_error()
    {
        // Arrange
        List<Activity> stoppedActivities = [];
        using var listener = ActivityBehaviorTestsHelpers.CreateCapturingListener(stoppedActivities);
        var behavior = new ActivityBehavior<ActivityTestQuery, int>();
        var request = new ActivityTestQuery(13);

        // Act
        await ((Func<Task>)(() =>
            behavior.Handle(
                request,
                static () => ValueTask.FromException<int>(new OperationCanceledException("unexpected")),
                CancellationToken.None).AsTask())).ShouldThrow<OperationCanceledException>();

        // Assert
        var activity = stoppedActivities.ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.StatusDescription.ShouldBeNull();
        activity.Tags.ShouldContain(static tag =>
            tag.Key == MediatorTelemetry.TagRuntimeOutcome && tag.Value == MediatorTelemetry.OutcomeError);
        activity.Tags.ShouldContain(static tag =>
            tag.Key == MediatorTelemetry.TagErrorType && tag.Value == nameof(OperationCanceledException));
        activity.Events.ShouldNotContain(static evt => evt.Name == "exception");
    }

    [Fact]
    public async Task Activity_behavior_does_not_record_an_error_when_the_handler_handles_the_exception_internally()
    {
        // Arrange
        List<Activity> stoppedActivities = [];
        using var listener = ActivityBehaviorTestsHelpers.CreateCapturingListener(stoppedActivities);
        var behavior = new ActivityBehavior<ActivityTestQuery, int>();
        var request = new ActivityTestQuery(14);

        // Act
        var response = await behavior.Handle(
            request,
            ActivityBehaviorTestsHelpers.HandleExceptionInternally,
            CancellationToken.None);

        // Assert
        (response).ShouldBe(99);
        var activity = (stoppedActivities).ShouldHaveSingleItem();
        (activity.Status).ShouldBe(ActivityStatusCode.Ok);
        (activity.StatusDescription).ShouldBeNull();
        (activity.Tags).ShouldContain(static tag => tag.Key == MediatorTelemetry.TagRuntimeOutcome && tag.Value == MediatorTelemetry.OutcomeSuccess);
        (activity.Tags).ShouldNotContain(static tag => tag.Key == MediatorTelemetry.TagErrorType);
        (activity.Events).ShouldNotContain(static evt => evt.Name == "exception");
    }

}
