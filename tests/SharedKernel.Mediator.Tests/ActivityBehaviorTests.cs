using System.Diagnostics;

namespace SharedKernel.Mediator.Tests;

/// <summary>
/// Tests that ActivityBehavior and the Mediator telemetry contract emit stable names, tags, and statuses for success, failure, cancellation, and absent listeners.
/// </summary>
[Trait(Testing.TestTraitNames.CapabilityName, TestTraits.ContractsCapability)]
public sealed class ActivityBehaviorTests
{
    [Fact]
    public void Listener_rejects_a_distinct_source_with_the_same_name()
    {
        // Arrange
        var activities = new List<Activity>();
        using var listener = ActivityBehaviorTestsHelpers.CreateCapturingListener(activities);
        using var impostor = new ActivitySource(MediatorActivitySource.ActivitySourceName);
        var predicate = listener.ShouldListenTo.ShouldNotBeNull();

        // Act
        var acceptsImpostor = predicate(impostor);
        var acceptsExpected = predicate(MediatorActivitySource.Source);

        // Assert
        acceptsImpostor.ShouldBeFalse();
        acceptsExpected.ShouldBeTrue();
    }

    [Fact]
    public void SharedKernel_mediator_activity_source_uses_stable_package_name()
    {
        // Arrange
        var expectedName = MediatorTelemetry.Name;

        // Act
        var activitySourceName = MediatorActivitySource.ActivitySourceName;
        var sourceName = MediatorActivitySource.Source.Name;

        // Assert
        activitySourceName.ShouldBe(expectedName);
        sourceName.ShouldBe(expectedName);
    }

    [Fact]
    public void Mediator_telemetry_contract_uses_stable_names()
    {
        // Act, Assert
        MediatorTelemetry.Name.ShouldBe("SharedKernel.Mediator");
        MediatorTelemetry.ActivitySend.ShouldBe("mediator.send");
        MediatorTelemetry.ActivityStream.ShouldBe("mediator.stream");
        MediatorTelemetry.ActivityPublish.ShouldBe("mediator.publish");
        MediatorTelemetry.ActivityNotificationHandler.ShouldBe("mediator.notification.handle");
        MediatorTelemetry.ActivityHandle.ShouldBe("mediator.handle");
        MediatorTelemetry.MetricRequests.ShouldBe("sharedkernel.mediator.requests");
        MediatorTelemetry.MetricRequestDuration.ShouldBe("sharedkernel.mediator.request.duration");
        MediatorTelemetry.MetricNotifications.ShouldBe("sharedkernel.mediator.notifications");
        MediatorTelemetry.MetricNotificationDuration.ShouldBe("sharedkernel.mediator.notification.duration");
        MediatorTelemetry.MetricStreams.ShouldBe("sharedkernel.mediator.streams");
        MediatorTelemetry.TagResponseName.ShouldBe("sharedkernel.mediator.response.name");
        MediatorTelemetry.TagRequestName.ShouldBe("sharedkernel.mediator.request.name");
        MediatorTelemetry.TagRequestAssembly.ShouldBe("sharedkernel.mediator.request.assembly");
        MediatorTelemetry.TagHandlerName.ShouldBe("sharedkernel.mediator.handler.name");
        MediatorTelemetry.TagPipelineDepth.ShouldBe("sharedkernel.mediator.pipeline.depth");
        MediatorTelemetry.TagNotificationName.ShouldBe("sharedkernel.mediator.notification.name");
        MediatorTelemetry.TagNotificationAssembly.ShouldBe("sharedkernel.mediator.notification.assembly");
        MediatorTelemetry.TagNotificationHandlerCount.ShouldBe("sharedkernel.mediator.notification.handler_count");
        MediatorTelemetry.TagOutcome.ShouldBe("sharedkernel.mediator.outcome");
        MediatorTelemetry.TagErrorType.ShouldBe("error.type");
        MediatorTelemetry.OutcomeSuccess.ShouldBe("success");
        MediatorTelemetry.OutcomeCancelled.ShouldBe("cancelled");
        MediatorTelemetry.OutcomeError.ShouldBe("error");
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
        response.ShouldBe(43);
        var activity = stoppedActivities.ShouldHaveSingleItem();
        activity.OperationName.ShouldBe("mediator.handle");
        activity.Source.Name.ShouldBe(MediatorActivitySource.ActivitySourceName);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.Tags.ShouldContain(static tag => tag.Key == "sharedkernel.mediator.request.name" && tag.Value == nameof(ActivityTestQuery));
        activity.Tags.ShouldContain(static tag => tag.Key == "sharedkernel.mediator.response.name" && tag.Value == nameof(Int32));
        activity.Tags.ShouldContain(static tag => tag.Key == MediatorTelemetry.TagOutcome && tag.Value == MediatorTelemetry.OutcomeSuccess);
        activity.Tags.ShouldNotContain(static tag => tag.Key == MediatorTelemetry.TagErrorType);
        activity.Events.ShouldNotContain(static evt => evt.Name == "exception");
    }

    [Fact]
    public async Task Activity_behavior_names_generic_types_without_their_arity()
    {
        // Arrange
        List<Activity> stoppedActivities = [];
        using var listener = ActivityBehaviorTestsHelpers.CreateCapturingListener(stoppedActivities);
        var behavior = new ActivityBehavior<ActivityTestListQuery, List<int>>();
        var request = new ActivityTestListQuery(3);

        // Act
        _ = await behavior.Handle(request, () => ValueTask.FromResult(new List<int> { request.Count }), CancellationToken.None);

        // Assert
        var activity = stoppedActivities.ShouldHaveSingleItem();
        activity.GetTagItem("sharedkernel.mediator.request.name").ShouldBe(nameof(ActivityTestListQuery));
        activity.GetTagItem("sharedkernel.mediator.response.name").ShouldBe("List");
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
        response.ShouldBe(14);
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
        await ShouldAssertionExtensions.ShouldThrow<InvalidOperationException>((Func<Task>)(() =>
            behavior.Handle(
                request,
                static () => ValueTask.FromException<int>(new InvalidOperationException("boom")),
                CancellationToken.None).AsTask()));

        // Assert
        var activity = stoppedActivities.ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.StatusDescription.ShouldBeNull();
        activity.Tags.ShouldContain(static tag => tag.Key == MediatorTelemetry.TagErrorType && tag.Value == "System.InvalidOperationException");
        activity.Tags.ShouldContain(static tag => tag.Key == MediatorTelemetry.TagOutcome && tag.Value == MediatorTelemetry.OutcomeError);

        activity.Events.ShouldNotContain(static evt => evt.Name == "exception");
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
        await ShouldAssertionExtensions.ShouldThrow<OperationCanceledException>((Func<Task>)(() =>
            behavior.Handle(
                request,
                () => ValueTask.FromException<int>(new OperationCanceledException(cancellation.Token)),
                cancellation.Token).AsTask()));

        // Assert
        var activity = stoppedActivities.ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Unset);
        activity.StatusDescription.ShouldBeNull();
        activity.Tags.ShouldContain(static tag => tag.Key == MediatorTelemetry.TagOutcome && tag.Value == MediatorTelemetry.OutcomeCancelled);
        activity.Tags.ShouldNotContain(static tag => tag.Key == MediatorTelemetry.TagErrorType);
        activity.Events.ShouldNotContain(static evt => evt.Name == "exception");
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
        await ShouldAssertionExtensions.ShouldThrow<OperationCanceledException>((Func<Task>)(() =>
            behavior.Handle(
                request,
                static () => ValueTask.FromException<int>(new OperationCanceledException("unexpected")),
                CancellationToken.None).AsTask()));

        // Assert
        var activity = stoppedActivities.ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.StatusDescription.ShouldBeNull();
        activity.Tags.ShouldContain(static tag =>
            tag.Key == MediatorTelemetry.TagOutcome && tag.Value == MediatorTelemetry.OutcomeError);
        activity.Tags.ShouldContain(static tag =>
            tag.Key == MediatorTelemetry.TagErrorType && tag.Value == "System.OperationCanceledException");
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
        response.ShouldBe(99);
        var activity = stoppedActivities.ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.StatusDescription.ShouldBeNull();
        activity.Tags.ShouldContain(static tag => tag.Key == MediatorTelemetry.TagOutcome && tag.Value == MediatorTelemetry.OutcomeSuccess);
        activity.Tags.ShouldNotContain(static tag => tag.Key == MediatorTelemetry.TagErrorType);
        activity.Events.ShouldNotContain(static evt => evt.Name == "exception");
    }

}
