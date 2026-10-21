using System.Diagnostics;

namespace PandaBear.Mediator.GeneratorTests;

internal static class GeneratorTelemetryBehaviorTestsHelpers
{
    public static ActivityListener CreateCapturingListener(List<Activity> stopped, ActivityTraceId traceId)
    {
        var expectedSource = SharedKernelMediatorActivitySource.Source;
        var listener = new ActivityListener
        {
            ShouldListenTo = source => ReferenceEquals(source, expectedSource),
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a =>
            {
                if (a.TraceId == traceId)
                {
                    stopped.Add(a);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
