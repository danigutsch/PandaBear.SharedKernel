using System.Diagnostics.Metrics;

namespace PandaBear;

/// <summary>
/// Holds the histogram bucket boundaries, in seconds, that every SharedKernel duration histogram advises, so durations from
/// different packages aggregate on the same buckets. This source file is linked into each package that records a duration.
/// </summary>
internal static class DurationBuckets
{
    /// <summary>Gets the advice that sets the shared bucket boundaries on a duration histogram created with unit <c>s</c>.</summary>
    public static InstrumentAdvice<double> Advice { get; } = new()
    {
        HistogramBucketBoundaries = [0.001, 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10],
    };
}
