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

    private sealed record TestQuery(int Id) : IQuery<int>;
}
