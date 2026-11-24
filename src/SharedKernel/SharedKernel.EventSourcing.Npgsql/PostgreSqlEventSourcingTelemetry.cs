using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PandaBear.EventSourcing.Npgsql;

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
    public static string ActivityAppend => "event_sourcing.append";

    /// <summary>
    /// Gets the activity name for stream load operations.
    /// </summary>
    public static string ActivityLoad => "event_sourcing.load";

    /// <summary>
    /// Gets the activity name for projection checkpoint operations.
    /// </summary>
    public static string ActivityCheckpoint => "event_sourcing.checkpoint";

    /// <summary>
    /// Gets the histogram name for append operation duration.
    /// </summary>
    public static string MetricAppendDuration => "sharedkernel.event_sourcing.append.duration";

    /// <summary>
    /// Gets the histogram name for load operation duration.
    /// </summary>
    public static string MetricLoadDuration => "sharedkernel.event_sourcing.load.duration";

    /// <summary>
    /// Gets the histogram name for projection checkpoint operation duration.
    /// </summary>
    public static string MetricCheckpointDuration => "sharedkernel.event_sourcing.checkpoint.duration";

    /// <summary>
    /// Gets the counter name for appended events.
    /// </summary>
    public static string MetricEventsAppended => "sharedkernel.event_sourcing.events.appended";

    /// <summary>
    /// Gets the counter name for loaded events.
    /// </summary>
    public static string MetricEventsLoaded => "sharedkernel.event_sourcing.events.loaded";

    /// <summary>
    /// Gets the counter name for optimistic concurrency conflicts.
    /// </summary>
    public static string MetricAppendConflicts => "sharedkernel.event_sourcing.append.conflicts";

    /// <summary>
    /// Gets the tag that captures the configured PostgreSQL schema name.
    /// </summary>
    public static string TagSchema => "sharedkernel.event_sourcing.schema";

    /// <summary>
    /// Gets the tag that captures the logical operation name.
    /// </summary>
    public static string TagOperation => "sharedkernel.event_sourcing.operation";

    /// <summary>
    /// Gets the tag that captures operation outcome.
    /// </summary>
    public static string TagOutcome => "sharedkernel.event_sourcing.outcome";

    /// <summary>
    /// Gets the tag that captures the number of events handled by an operation.
    /// </summary>
    public static string TagEventCount => "sharedkernel.event_sourcing.event_count";

    /// <summary>
    /// Gets the tag that captures the expected stream revision mode.
    /// </summary>
    public static string TagExpectedRevisionMode => "sharedkernel.event_sourcing.expected_revision.mode";

    /// <summary>
    /// Gets the tag that captures the expected stream revision value when one is required.
    /// </summary>
    public static string TagExpectedRevisionValue => "sharedkernel.event_sourcing.expected_revision.value";

    /// <summary>
    /// Gets the tag that captures the actual stream revision during conflicts.
    /// </summary>
    public static string TagActualRevision => "sharedkernel.event_sourcing.actual_revision";

    /// <summary>
    /// Gets the tag that captures projection checkpoint names.
    /// </summary>
    public static string TagProjectionName => "sharedkernel.event_sourcing.projection.name";

    /// <summary>
    /// Gets the tag that captures projection checkpoint positions.
    /// </summary>
    public static string TagCheckpointPosition => "sharedkernel.event_sourcing.checkpoint.position";

    /// <summary>
    /// Gets the semantic-convention tag that names the database system on every span and measurement.
    /// </summary>
    public static string TagDbSystemName => "db.system.name";

    /// <summary>
    /// Gets the <see cref="TagDbSystemName" /> value of the PostgreSQL provider.
    /// </summary>
    public static string DbSystemNamePostgreSql => "postgresql";

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

    /// <summary>Source of the event sourcing activities.</summary>
    internal static ActivitySource ActivitySource { get; } = new(Name);

    /// <summary>Meter that owns the event sourcing instruments.</summary>
    internal static Meter Meter { get; } = new(Name);

    /// <summary>Records how long an event stream append took, in seconds.</summary>
    internal static Histogram<double> AppendDuration { get; } = Meter.CreateHistogram(
        MetricAppendDuration,
        unit: "s",
        description: "Duration of PostgreSQL event stream append operations.",
        advice: DurationBuckets.Advice);

    /// <summary>Records how long an event stream load took, in seconds.</summary>
    internal static Histogram<double> LoadDuration { get; } = Meter.CreateHistogram(
        MetricLoadDuration,
        unit: "s",
        description: "Duration of PostgreSQL event stream load operations.",
        advice: DurationBuckets.Advice);

    /// <summary>Records how long a projection checkpoint operation took, in seconds.</summary>
    internal static Histogram<double> CheckpointDuration { get; } = Meter.CreateHistogram(
        MetricCheckpointDuration,
        unit: "s",
        description: "Duration of PostgreSQL projection checkpoint operations.",
        advice: DurationBuckets.Advice);

    /// <summary>Counts events written to event streams.</summary>
    internal static Counter<long> EventsAppended { get; } = Meter.CreateCounter<long>(
        MetricEventsAppended,
        unit: "{event}",
        description: "Number of events appended to PostgreSQL event streams.");

    /// <summary>Counts events read from event streams.</summary>
    internal static Counter<long> EventsLoaded { get; } = Meter.CreateCounter<long>(
        MetricEventsLoaded,
        unit: "{event}",
        description: "Number of events loaded from PostgreSQL event streams.");

    /// <summary>Counts appends rejected because the expected stream revision did not match.</summary>
    internal static Counter<long> AppendConflicts { get; } = Meter.CreateCounter<long>(
        MetricAppendConflicts,
        unit: "{conflict}",
        description: "Number of PostgreSQL event stream append conflicts.");
}
