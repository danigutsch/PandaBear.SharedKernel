using System.Diagnostics;

namespace PandaBear.Mediator.GeneratorTests;

/// <summary>Helpers for telemetry tests of the generated mediator.</summary>
internal static class GeneratorTelemetryBehaviorTestsHelpers
{
    /// <summary>Creates and registers an ActivityListener that records every sample of the mediator activity source and adds each stopped activity with the given trace id to the list.</summary>
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
