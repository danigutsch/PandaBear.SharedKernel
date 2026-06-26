using System.Diagnostics;

namespace SharedKernel.Mediator.GeneratorTests;

internal static class GeneratorTelemetryBehaviorTestsHelpers
{
    public static ActivityListener CreateCapturingListener(List<Activity> stopped, ActivityTraceId traceId)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == SharedKernelMediatorActivitySource.ActivitySourceName,
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
