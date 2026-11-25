using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SharedKernel.Scheduling;

/// <summary>
/// Defines stable telemetry names emitted by SharedKernel scheduling primitives.
/// </summary>
public static class SchedulingTelemetry
{
    private static readonly ConcurrentDictionary<BatchDrainExecution, (string ServiceName, CancellationToken StoppingToken)> BatchDrains =
        new(ReferenceEqualityComparer.Instance);

    static SchedulingTelemetry()
    {
        _ = Meter.CreateObservableGauge(
            MetricPollingBatchActiveDuration,
            ObservePollingBatchActiveDurations,
            "s",
            "Elapsed duration of the longest active batch for each running polling service.");
    }

    /// <summary>
    /// Gets the shared activity source and meter name for scheduling operations.
    /// </summary>
    public static string Name => "SharedKernel.Scheduling";

    /// <summary>
    /// Gets the activity name for one polling drain cycle.
    /// </summary>
    public static string ActivityPollingCycle => "scheduling.poll";

    /// <summary>
    /// Gets the histogram name for polling cycle duration.
    /// </summary>
    public static string MetricPollingCycleDuration => "sharedkernel.scheduling.polling.cycle.duration";

    /// <summary>
    /// Gets the gauge name for elapsed time in a running polling service's current batch.
    /// </summary>
    public static string MetricPollingBatchActiveDuration => "sharedkernel.scheduling.polling.batch.active.duration";

    /// <summary>
    /// Gets the counter name for completed polling batches.
    /// </summary>
    public static string MetricPollingBatches => "sharedkernel.scheduling.polling.batches";

    /// <summary>
    /// Gets the counter name for processed polling work items.
    /// </summary>
    public static string MetricPollingItems => "sharedkernel.scheduling.polling.items";

    /// <summary>
    /// Gets the counter name for non-fatal polling failures.
    /// </summary>
    public static string MetricPollingFailures => "sharedkernel.scheduling.polling.failures";

    /// <summary>
    /// Gets the tag that captures the polling service name.
    /// </summary>
    public static string TagServiceName => "sharedkernel.scheduling.service.name";

    /// <summary>
    /// Gets the tag that captures operation outcome.
    /// </summary>
    public static string TagOutcome => "sharedkernel.scheduling.outcome";

    /// <summary>
    /// Gets the tag that captures processed item count.
    /// </summary>
    public static string TagItemCount => "sharedkernel.scheduling.item_count";

    /// <summary>
    /// Gets the semantic-convention tag that captures the full exception type name of failed polling work.
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

    /// <summary>Source of the polling activities.</summary>
    internal static ActivitySource ActivitySource { get; } = new(Name);

    /// <summary>Meter that owns the polling instruments.</summary>
    internal static Meter Meter { get; } = new(Name);

    /// <summary>Records how long one polling drain cycle took, in seconds.</summary>
    internal static Histogram<double> PollingCycleDuration { get; } = Meter.CreateHistogram(
        MetricPollingCycleDuration,
        "s",
        "Duration of one polling drain cycle.",
        advice: DurationBuckets.Advice);

    /// <summary>Counts polling batches that completed.</summary>
    internal static Counter<long> PollingBatches { get; } = Meter.CreateCounter<long>(
        MetricPollingBatches,
        "{batch}",
        "Completed polling batches.");

    /// <summary>Counts work items processed by polling.</summary>
    internal static Counter<long> PollingItems { get; } = Meter.CreateCounter<long>(
        MetricPollingItems,
        "{item}",
        "Processed polling work items.");

    /// <summary>Counts polling failures that were handled without stopping the poller.</summary>
    internal static Counter<long> PollingFailures { get; } = Meter.CreateCounter<long>(
        MetricPollingFailures,
        "{failure}",
        "Non-fatal polling failures.");

    /// <summary>Begins observing a drain for the duration of its owner's lifetime.</summary>
    internal static void RegisterBatchDrain(BatchDrainExecution drain, string serviceName, CancellationToken stoppingToken) =>
        BatchDrains.TryAdd(drain, (serviceName, stoppingToken));

    /// <summary>Stops observing a drain whose owner has stopped.</summary>
    internal static void UnregisterBatchDrain(BatchDrainExecution drain) =>
        BatchDrains.TryRemove(drain, out _);

    private static IEnumerable<Measurement<double>> ObservePollingBatchActiveDurations() =>
        BatchDrains
            .Where(entry => !entry.Value.StoppingToken.IsCancellationRequested)
            .GroupBy(entry => entry.Value.ServiceName, StringComparer.Ordinal)
            .Select(group => new Measurement<double>(
                group.Max(entry => entry.Key.GetActiveBatchDurationSeconds()),
                new KeyValuePair<string, object?>(TagServiceName, group.Key)));
}
