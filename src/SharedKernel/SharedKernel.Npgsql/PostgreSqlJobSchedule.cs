namespace SharedKernel.Npgsql;

/// <summary>Describes the persisted schedule for one job.</summary>
public sealed class PostgreSqlJobSchedule
{
    /// <summary>Captures a durable schedule read by the provider.</summary>
    internal PostgreSqlJobSchedule(string jobName, DateTimeOffset? nextDueAt, TimeSpan? fixedDelay, bool isPaused)
    {
        JobName = jobName;
        NextDueAt = nextDueAt;
        FixedDelay = fixedDelay;
        IsPaused = isPaused;
    }

    /// <summary>Gets the configured job name.</summary>
    public string JobName { get; }

    /// <summary>Gets the next scheduled due time when one is configured and available.</summary>
    public DateTimeOffset? NextDueAt { get; }

    /// <summary>Gets the elapsed delay after a scheduled completion, when configured.</summary>
    public TimeSpan? FixedDelay { get; }

    /// <summary>Gets whether the job is paused for acquisition.</summary>
    public bool IsPaused { get; }
}
