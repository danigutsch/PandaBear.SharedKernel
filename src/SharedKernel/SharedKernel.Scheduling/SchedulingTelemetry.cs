using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SharedKernel.Scheduling;

/// <summary>
/// Defines stable telemetry names emitted by SharedKernel scheduling primitives.
/// </summary>
public static class SchedulingTelemetry
{
    /// <summary>
    /// Gets the shared activity source and meter name for scheduling operations.
    /// </summary>
    public static string Name => "SharedKernel.Scheduling";

    /// <summary>
    /// Gets the activity name for one polling drain cycle.
    /// </summary>
    public static string ActivityPollingCycle => "poll";

    /// <summary>
    /// Gets the histogram name for polling cycle duration.
    /// </summary>
    public static string MetricPollingCycleDuration => "scheduling.polling.cycle.duration";

    /// <summary>
    /// Gets the counter name for completed polling batches.
    /// </summary>
    public static string MetricPollingBatches => "scheduling.polling.batch";

    /// <summary>
    /// Gets the counter name for processed polling work items.
    /// </summary>
    public static string MetricPollingItems => "scheduling.polling.item";

    /// <summary>
    /// Gets the counter name for non-fatal polling failures.
    /// </summary>
    public static string MetricPollingFailures => "scheduling.polling.failure";

    /// <summary>
    /// Gets the tag that captures the polling service name.
    /// </summary>
    public static string TagServiceName => "scheduling.service.name";

    /// <summary>
    /// Gets the tag that captures operation outcome.
    /// </summary>
    public static string TagOutcome => "scheduling.outcome";

    /// <summary>
    /// Gets the tag that captures processed item count.
    /// </summary>
    public static string TagItemCount => "scheduling.item.count";

    /// <summary>
    /// Gets the tag that captures the exception type for failed polling work.
    /// </summary>
    public static string TagErrorType => "error.type";

    /// <summary>
    /// Gets the success outcome value.
    /// </summary>
    public static string OutcomeSuccess => "success";

    /// <summary>
    /// Gets the cancelled outcome value.
    /// </summary>
    public static string OutcomeCancelled => "cancelled";

    /// <summary>
    /// Gets the error outcome value.
    /// </summary>
    public static string OutcomeError => "error";

    internal static ActivitySource ActivitySource { get; } = new(Name);

    internal static Meter Meter { get; } = new(Name);

    internal static Histogram<double> PollingCycleDuration { get; } = Meter.CreateHistogram<double>(
        MetricPollingCycleDuration,
        "s",
        "Duration of one polling drain cycle.");

    internal static Counter<long> PollingBatches { get; } = Meter.CreateCounter<long>(
        MetricPollingBatches,
        "{batch}",
        "Completed polling batches.");

    internal static Counter<long> PollingItems { get; } = Meter.CreateCounter<long>(
        MetricPollingItems,
        "{item}",
        "Processed polling work items.");

    internal static Counter<long> PollingFailures { get; } = Meter.CreateCounter<long>(
        MetricPollingFailures,
        "{failure}",
        "Non-fatal polling failures.");
}
