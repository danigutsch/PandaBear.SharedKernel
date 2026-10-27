using System.Diagnostics;

namespace PandaBear.Mediator.Tests;

/// <summary>
/// Shared helpers for the activity behavior tests: a capturing listener and a handler that swallows its own exception.
/// </summary>
internal static class ActivityBehaviorTestsHelpers
{
    /// <summary>
    /// Creates and registers an activity listener that samples everything from the Mediator activity source only and appends each stopped activity to the supplied list.
    /// </summary>
    /// <param name="stoppedActivities">The list that receives every activity as it stops.</param>
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

    /// <summary>
    /// Simulates a handler that throws and catches an InvalidOperationException itself and returns 99, so no exception reaches the behavior.
    /// </summary>
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
