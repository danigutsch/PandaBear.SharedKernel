using System.Diagnostics;

namespace SharedKernel.Mediator.Tests;

[Trait(TestTraits.CapabilityName, TestTraits.ContractsCapability)]
public sealed class ActivityBehaviorTests
{
    [Fact]
    public void SharedKernel_Mediator_Activity_Source_Uses_Stable_Package_Name()
    {
        // Arrange
        const string expectedName = "SharedKernel.Mediator";

        // Act
        var activitySourceName = SharedKernelMediatorActivitySource.ActivitySourceName;
        var sourceName = SharedKernelMediatorActivitySource.Source.Name;

        // Assert
        Assert.Equal(expectedName, activitySourceName);
        Assert.Equal(expectedName, sourceName);
    }

    [Fact]
    public async Task Activity_Behavior_Starts_Request_Activity_When_A_Listener_Is_Registered()
    {
        // Arrange
        List<Activity> stoppedActivities = [];
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == SharedKernelMediatorActivitySource.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = static (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stoppedActivities.Add,
        };
        ActivitySource.AddActivityListener(listener);
        var behavior = new ActivityBehavior<TestQuery, int>();
        var request = new TestQuery(42);

        // Act
        var response = await behavior.Handle(request, () => ValueTask.FromResult(request.Id + 1), CancellationToken.None);

        // Assert
        Assert.Equal(43, response);
        var activity = Assert.Single(stoppedActivities);
        Assert.Equal(nameof(TestQuery), activity.OperationName);
        Assert.Equal(SharedKernelMediatorActivitySource.ActivitySourceName, activity.Source.Name);
        Assert.Contains(activity.Tags, static tag => tag.Key == "sharedkernel.mediator.request_type" && tag.Value == typeof(TestQuery).FullName);
        Assert.Contains(activity.Tags, static tag => tag.Key == "sharedkernel.mediator.response_type" && tag.Value == typeof(int).FullName);
    }

    [Fact]
    public async Task Activity_Behavior_Completes_When_No_Listener_Is_Registered()
    {
        // Arrange
        var behavior = new ActivityBehavior<TestQuery, int>();
        var request = new TestQuery(7);

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
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == SharedKernelMediatorActivitySource.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = static (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stoppedActivities.Add,
        };
        ActivitySource.AddActivityListener(listener);
        var behavior = new ActivityBehavior<TestQuery, int>();
        var request = new TestQuery(11);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(
                request,
                static () => ValueTask.FromException<int>(new InvalidOperationException("boom")),
                CancellationToken.None).AsTask());

        // Assert
        var activity = Assert.Single(stoppedActivities);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Contains(activity.Tags, static tag => tag.Key == "error.type" && tag.Value == "InvalidOperationException");
        Assert.Contains(activity.Tags, static tag => tag.Key == "sharedkernel.mediator.outcome" && tag.Value == "error");

        var exceptionEvent = Assert.Single(activity.Events, static evt => evt.Name == "exception");
        Assert.Contains(exceptionEvent.Tags!, static tag =>
            tag.Key == "exception.type" && string.Equals(tag.Value as string, typeof(InvalidOperationException).FullName, StringComparison.Ordinal));
        Assert.Contains(exceptionEvent.Tags!, static tag =>
            tag.Key == "exception.message" && string.Equals(tag.Value as string, "boom", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Activity_Behavior_Does_Not_Record_Exception_Event_When_The_Handler_Is_Cancelled()
    {
        // Arrange
        List<Activity> stoppedActivities = [];
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == SharedKernelMediatorActivitySource.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = static (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stoppedActivities.Add,
        };
        ActivitySource.AddActivityListener(listener);
        var behavior = new ActivityBehavior<TestQuery, int>();
        var request = new TestQuery(12);

        // Act
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            behavior.Handle(
                request,
                static () => ValueTask.FromException<int>(new OperationCanceledException("cancelled")),
                CancellationToken.None).AsTask());

        // Assert
        var activity = Assert.Single(stoppedActivities);
        Assert.Equal(ActivityStatusCode.Ok, activity.Status);
        Assert.Contains(activity.Tags, static tag => tag.Key == "sharedkernel.mediator.outcome" && tag.Value == "cancelled");
        Assert.DoesNotContain(activity.Tags, static tag => tag.Key == "error.type");
        Assert.DoesNotContain(activity.Events, static evt => evt.Name == "exception");
    }

    private sealed record TestQuery(int Id) : IQuery<int>;
}
