using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SharedKernel.EventSourcing.Npgsql;

/// <summary>
/// Defines stable telemetry names emitted by the PostgreSQL event-sourcing provider.
/// </summary>
public static class PostgreSqlEventSourcingTelemetry
{
    /// <summary>
    /// Gets the shared activity source and meter name for PostgreSQL event-sourcing operations.
    /// </summary>
    public static string Name => "SharedKernel.EventSourcing.Npgsql";

    /// <summary>
    /// Gets the activity name for stream append operations.
    /// </summary>
    public static string ActivityAppend => "eventsourcing.postgresql.append";

    /// <summary>
    /// Gets the activity name for stream load operations.
    /// </summary>
    public static string ActivityLoad => "eventsourcing.postgresql.load";

    /// <summary>
    /// Gets the activity name for projection checkpoint operations.
    /// </summary>
    public static string ActivityCheckpoint => "eventsourcing.postgresql.checkpoint";

    /// <summary>
    /// Gets the histogram name for append operation duration.
    /// </summary>
    public static string MetricAppendDuration => "eventsourcing.postgresql.append.duration";

    /// <summary>
    /// Gets the histogram name for load operation duration.
    /// </summary>
    public static string MetricLoadDuration => "eventsourcing.postgresql.load.duration";

    /// <summary>
    /// Gets the histogram name for projection checkpoint operation duration.
    /// </summary>
    public static string MetricCheckpointDuration => "eventsourcing.postgresql.checkpoint.duration";

    /// <summary>
    /// Gets the counter name for appended events.
    /// </summary>
    public static string MetricEventsAppended => "eventsourcing.postgresql.event.appended";

    /// <summary>
    /// Gets the counter name for loaded events.
    /// </summary>
    public static string MetricEventsLoaded => "eventsourcing.postgresql.event.loaded";

    /// <summary>
    /// Gets the counter name for optimistic concurrency conflicts.
    /// </summary>
    public static string MetricAppendConflicts => "eventsourcing.postgresql.append.conflict";

    /// <summary>
    /// Gets the tag that captures the configured PostgreSQL schema name.
    /// </summary>
    public static string TagSchema => "eventsourcing.postgresql.schema";

    /// <summary>
    /// Gets the tag that captures the logical operation name.
    /// </summary>
    public static string TagOperation => "eventsourcing.operation";

    /// <summary>
    /// Gets the tag that captures operation outcome.
    /// </summary>
    public static string TagOutcome => "eventsourcing.outcome";

    /// <summary>
    /// Gets the tag that captures the number of events handled by an operation.
    /// </summary>
    public static string TagEventCount => "eventsourcing.event.count";

    /// <summary>
    /// Gets the tag that captures the expected stream revision mode.
    /// </summary>
    public static string TagExpectedRevisionMode => "eventsourcing.expected_revision.mode";

    /// <summary>
    /// Gets the tag that captures the expected stream revision value when one is required.
    /// </summary>
    public static string TagExpectedRevisionValue => "eventsourcing.expected_revision.value";

    /// <summary>
    /// Gets the tag that captures the actual stream revision during conflicts.
    /// </summary>
    public static string TagActualRevision => "eventsourcing.actual_revision";

    /// <summary>
    /// Gets the tag that captures projection checkpoint names.
    /// </summary>
    public static string TagProjectionName => "eventsourcing.projection.name";

    /// <summary>
    /// Gets the tag that captures projection checkpoint positions.
    /// </summary>
    public static string TagCheckpointPosition => "eventsourcing.checkpoint.position";

    /// <summary>
    /// Gets the success outcome value.
    /// </summary>
    public static string OutcomeSuccess => "success";

    /// <summary>
    /// Gets the conflict outcome value.
    /// </summary>
    public static string OutcomeConflict => "conflict";

    /// <summary>
    /// Gets the error outcome value.
    /// </summary>
    public static string OutcomeError => "error";

    /// <summary>
    /// Gets the expected revision mode that accepts any stream revision.
    /// </summary>
    public static string ExpectedRevisionAny => "any";

    /// <summary>
    /// Gets the expected revision mode that requires a missing or empty stream.
    /// </summary>
    public static string ExpectedRevisionNoStream => "no_stream";

    /// <summary>
    /// Gets the expected revision mode that requires a specific stream revision.
    /// </summary>
    public static string ExpectedRevisionExact => "exact";

    internal static ActivitySource ActivitySource { get; } = new(Name);

    internal static Meter Meter { get; } = new(Name);

    internal static Histogram<double> AppendDuration { get; } = Meter.CreateHistogram<double>(
        MetricAppendDuration,
        unit: "ms",
        description: "Duration of PostgreSQL event stream append operations.");

    internal static Histogram<double> LoadDuration { get; } = Meter.CreateHistogram<double>(
        MetricLoadDuration,
        unit: "ms",
        description: "Duration of PostgreSQL event stream load operations.");

    internal static Histogram<double> CheckpointDuration { get; } = Meter.CreateHistogram<double>(
        MetricCheckpointDuration,
        unit: "ms",
        description: "Duration of PostgreSQL projection checkpoint operations.");

    internal static Counter<long> EventsAppended { get; } = Meter.CreateCounter<long>(
        MetricEventsAppended,
        unit: "{event}",
        description: "Number of events appended to PostgreSQL event streams.");

    internal static Counter<long> EventsLoaded { get; } = Meter.CreateCounter<long>(
        MetricEventsLoaded,
        unit: "{event}",
        description: "Number of events loaded from PostgreSQL event streams.");

    internal static Counter<long> AppendConflicts { get; } = Meter.CreateCounter<long>(
        MetricAppendConflicts,
        unit: "{conflict}",
        description: "Number of PostgreSQL event stream append conflicts.");
}
