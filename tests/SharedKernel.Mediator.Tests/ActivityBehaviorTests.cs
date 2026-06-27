using System.Diagnostics;

namespace SharedKernel.Mediator.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ContractsCapability)]
public sealed class ActivityBehaviorTests
{
    [Fact]
    public void SharedKernel_Mediator_Activity_Source_Uses_Stable_Package_Name()
    {
        // Arrange
        var expectedName = MediatorTelemetry.Name;

        // Act
        var activitySourceName = SharedKernelMediatorActivitySource.ActivitySourceName;
        var sourceName = SharedKernelMediatorActivitySource.Source.Name;

        // Assert
        Assert.Equal(expectedName, activitySourceName);
        Assert.Equal(expectedName, sourceName);
    }

    [Fact]
    public void Mediator_Telemetry_Contract_Uses_Stable_Names()
    {
        // Assert
        Assert.Equal("SharedKernel.Mediator", MediatorTelemetry.Name);
        Assert.Equal("mediator.send", MediatorTelemetry.ActivitySend);
        Assert.Equal("mediator.stream", MediatorTelemetry.ActivityStream);
        Assert.Equal("mediator.publish", MediatorTelemetry.ActivityPublish);
        Assert.Equal("mediator.notification.handle", MediatorTelemetry.ActivityNotificationHandler);
        Assert.Equal("mediator.requests", MediatorTelemetry.MetricRequests);
        Assert.Equal("mediator.request.duration", MediatorTelemetry.MetricRequestDuration);
        Assert.Equal("mediator.notifications", MediatorTelemetry.MetricNotifications);
        Assert.Equal("mediator.notification.duration", MediatorTelemetry.MetricNotificationDuration);
        Assert.Equal("mediator.streams", MediatorTelemetry.MetricStreams);
        Assert.Equal("sharedkernel.mediator.request_type", MediatorTelemetry.TagRequestType);
        Assert.Equal("sharedkernel.mediator.response_type", MediatorTelemetry.TagResponseType);
        Assert.Equal("sharedkernel.mediator.outcome", MediatorTelemetry.TagRuntimeOutcome);
        Assert.Equal("mediator.request.name", MediatorTelemetry.TagRequestName);
        Assert.Equal("mediator.request.assembly", MediatorTelemetry.TagRequestAssembly);
        Assert.Equal("mediator.handler.name", MediatorTelemetry.TagHandlerName);
        Assert.Equal("mediator.pipeline.depth", MediatorTelemetry.TagPipelineDepth);
        Assert.Equal("mediator.notification.name", MediatorTelemetry.TagNotificationName);
        Assert.Equal("mediator.notification.assembly", MediatorTelemetry.TagNotificationAssembly);
        Assert.Equal("mediator.notification.handler.count", MediatorTelemetry.TagNotificationHandlerCount);
        Assert.Equal("mediator.outcome", MediatorTelemetry.TagOutcome);
        Assert.Equal("error.type", MediatorTelemetry.TagErrorType);
        Assert.Equal("success", MediatorTelemetry.OutcomeSuccess);
        Assert.Equal("cancelled", MediatorTelemetry.OutcomeCancelled);
        Assert.Equal("error", MediatorTelemetry.OutcomeError);
    }

    [Fact]
    public async Task Activity_Behavior_Starts_Request_Activity_When_A_Listener_Is_Registered()
    {
        // Arrange
        List<Activity> stoppedActivities = [];
        using var listener = ActivityBehaviorTestsHelpers.CreateCapturingListener(stoppedActivities);
        var behavior = new ActivityBehavior<ActivityTestQuery, int>();
        var request = new ActivityTestQuery(42);

        // Act
        var response = await behavior.Handle(request, () => ValueTask.FromResult(request.Id + 1), CancellationToken.None);

        // Assert
        Assert.Equal(43, response);
        var activity = Assert.Single(stoppedActivities);
        Assert.Equal(nameof(ActivityTestQuery), activity.OperationName);
        Assert.Equal(SharedKernelMediatorActivitySource.ActivitySourceName, activity.Source.Name);
        Assert.Equal(ActivityStatusCode.Ok, activity.Status);
        Assert.Contains(activity.Tags, static tag => tag.Key == MediatorTelemetry.TagRequestType && tag.Value == typeof(ActivityTestQuery).FullName);
        Assert.Contains(activity.Tags, static tag => tag.Key == MediatorTelemetry.TagResponseType && tag.Value == typeof(int).FullName);
        Assert.Contains(activity.Tags, static tag => tag.Key == MediatorTelemetry.TagRuntimeOutcome && tag.Value == MediatorTelemetry.OutcomeSuccess);
        Assert.DoesNotContain(activity.Tags, static tag => tag.Key == MediatorTelemetry.TagErrorType);
        Assert.DoesNotContain(activity.Events, static evt => evt.Name == "exception");
    }

    [Fact]
    public async Task Activity_Behavior_Completes_When_No_Listener_Is_Registered()
    {
        // Arrange
        var behavior = new ActivityBehavior<ActivityTestQuery, int>();
        var request = new ActivityTestQuery(7);

        // Act
        var response = await behavior.Handle(request, () => ValueTask.FromResult(request.Id * 2), CancellationToken.None);

        // Assert
        Assert.Equal(14, response);
    }

    [Fact]
    public async Task Activity_Behavior_Records_Exception_Event_When_The_Handler_Fails()
    {
        // Arrange
        List<Activity> stoppedActivities = [];
        using var listener = ActivityBehaviorTestsHelpers.CreateCapturingListener(stoppedActivities);
        var behavior = new ActivityBehavior<ActivityTestQuery, int>();
        var request = new ActivityTestQuery(11);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(
                request,
                static () => ValueTask.FromException<int>(new InvalidOperationException("boom")),
                CancellationToken.None).AsTask());

        // Assert
        var activity = Assert.Single(stoppedActivities);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("boom", activity.StatusDescription);
        Assert.Contains(activity.Tags, static tag => tag.Key == MediatorTelemetry.TagErrorType && tag.Value == "InvalidOperationException");
        Assert.Contains(activity.Tags, static tag => tag.Key == MediatorTelemetry.TagRuntimeOutcome && tag.Value == MediatorTelemetry.OutcomeError);

        var exceptionEvent = Assert.Single(activity.Events, static evt => evt.Name == "exception");
        var exceptionTags = exceptionEvent.Tags;
        Assert.NotNull(exceptionTags);
        Assert.Contains(exceptionTags, static tag =>
            tag.Key == "exception.type" && string.Equals(tag.Value as string, typeof(InvalidOperationException).FullName, StringComparison.Ordinal));
        Assert.Contains(exceptionTags, static tag =>
            tag.Key == "exception.message" && string.Equals(tag.Value as string, "boom", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Activity_Behavior_Does_Not_Record_Exception_Event_When_The_Handler_Is_Cancelled()
    {
        // Arrange
        List<Activity> stoppedActivities = [];
        using var listener = ActivityBehaviorTestsHelpers.CreateCapturingListener(stoppedActivities);
        var behavior = new ActivityBehavior<ActivityTestQuery, int>();
        var request = new ActivityTestQuery(12);

        // Act
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            behavior.Handle(
                request,
                static () => ValueTask.FromException<int>(new OperationCanceledException("cancelled")),
                CancellationToken.None).AsTask());

        // Assert
        var activity = Assert.Single(stoppedActivities);
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        Assert.Null(activity.StatusDescription);
        Assert.Contains(activity.Tags, static tag => tag.Key == MediatorTelemetry.TagRuntimeOutcome && tag.Value == MediatorTelemetry.OutcomeCancelled);
        Assert.DoesNotContain(activity.Tags, static tag => tag.Key == MediatorTelemetry.TagErrorType);
        Assert.DoesNotContain(activity.Events, static evt => evt.Name == "exception");
    }

    [Fact]
    public async Task Activity_Behavior_Does_Not_Record_An_Error_When_The_Handler_Handles_The_Exception_Internally()
    {
        // Arrange
        List<Activity> stoppedActivities = [];
        using var listener = ActivityBehaviorTestsHelpers.CreateCapturingListener(stoppedActivities);
        var behavior = new ActivityBehavior<ActivityTestQuery, int>();
        var request = new ActivityTestQuery(13);

        // Act
        var response = await behavior.Handle(
            request,
            static () =>
            {
                try
                {
                    throw new InvalidOperationException("boom");
                }
                catch (InvalidOperationException)
                {
                    return ValueTask.FromResult(99);
                }
            },
            CancellationToken.None);

        // Assert
        Assert.Equal(99, response);
        var activity = Assert.Single(stoppedActivities);
        Assert.Equal(ActivityStatusCode.Ok, activity.Status);
        Assert.Null(activity.StatusDescription);
        Assert.Contains(activity.Tags, static tag => tag.Key == MediatorTelemetry.TagRuntimeOutcome && tag.Value == MediatorTelemetry.OutcomeSuccess);
        Assert.DoesNotContain(activity.Tags, static tag => tag.Key == MediatorTelemetry.TagErrorType);
        Assert.DoesNotContain(activity.Events, static evt => evt.Name == "exception");
    }

}
