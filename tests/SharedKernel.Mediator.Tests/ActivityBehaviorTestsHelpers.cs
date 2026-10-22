using System.Diagnostics;

namespace PandaBear.Mediator.Tests;

internal static class ActivityBehaviorTestsHelpers
{
    public static ActivityListener CreateCapturingListener(List<Activity> stoppedActivities)
    {
        var expectedSource = SharedKernelMediatorActivitySource.Source;
        var listener = new ActivityListener
        {
            ShouldListenTo = source => ReferenceEquals(source, expectedSource),
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = static (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stoppedActivities.Add,
        };

        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    public static ValueTask<int> HandleExceptionInternally()
    {
        try
        {
            throw new InvalidOperationException("boom");
        }
        catch (InvalidOperationException)
        {
            return ValueTask.FromResult(99);
        }
    }
}
