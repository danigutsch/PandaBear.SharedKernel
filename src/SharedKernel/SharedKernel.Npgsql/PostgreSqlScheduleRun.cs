namespace PandaBear.Npgsql;

/// <summary>Contains scheduling metadata for one observed run.</summary>
/// <param name="RunId">The run's opaque identifier.</param>
/// <param name="StartedAt">The database time when the start observation was recorded.</param>
/// <param name="ScheduledFor">The provider's intended fire time when known.</param>
/// <param name="FinishedAt">The database time when a completion was recorded, when known.</param>
/// <param name="RetryAt">The provider's recorded retry time for a retry-scheduled outcome.</param>
/// <param name="Status">The state supported by the current durable observation.</param>
public readonly record struct PostgreSqlScheduleRun(
    Guid RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset? ScheduledFor,
    DateTimeOffset? FinishedAt,
    DateTimeOffset? RetryAt,
    PostgreSqlScheduleRunStatus Status);
