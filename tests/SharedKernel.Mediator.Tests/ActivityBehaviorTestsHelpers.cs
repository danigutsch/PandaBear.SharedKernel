using System.Diagnostics;

namespace SharedKernel.Mediator.Tests;

internal static class ActivityBehaviorTestsHelpers
{
    public static ActivityListener CreateCapturingListener(List<Activity> stoppedActivities)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == SharedKernelMediatorActivitySource.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = static (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stoppedActivities.Add,
        };

        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
