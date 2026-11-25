namespace SharedKernel.Npgsql;

/// <summary>Identifies the current owner of one durable job execution.</summary>
public sealed class PostgreSqlJobLease
{
    /// <summary>Captures a fence issued by the database when acquiring a run.</summary>
    internal PostgreSqlJobLease(
        string jobName, Guid runId, Guid fence, DateTimeOffset expiresAt, DateTimeOffset? scheduledFor, PostgreSqlJobRunKind kind)
    {
        JobName = jobName;
        RunId = runId;
        Fence = fence;
        ExpiresAt = expiresAt;
        ScheduledFor = scheduledFor;
        Kind = kind;
    }

    /// <summary>Gets the configured job name.</summary>
    public string JobName { get; }

    /// <summary>Gets the durable execution identifier, which stays the same when the lease expires and is reacquired.</summary>
    public Guid RunId { get; }

    /// <summary>Gets the ownership fence issued for this acquisition.</summary>
    public Guid Fence { get; }

    /// <summary>Gets the database's expiration at acquisition. A subsequent renewal may extend it.</summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>Gets the originally due time for a scheduled execution.</summary>
    public DateTimeOffset? ScheduledFor { get; }

    /// <summary>Gets the reason for the execution.</summary>
    public PostgreSqlJobRunKind Kind { get; }
}
