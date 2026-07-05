namespace SharedKernel.Idempotency.EntityFrameworkCore;

/// <summary>
/// Represents a persisted idempotency entry.
/// </summary>
internal sealed class IdempotencyEntryEntity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IdempotencyEntryEntity" /> class.
    /// </summary>
    /// <param name="scope">The operation scope.</param>
    /// <param name="key">The operation key.</param>
    /// <param name="startedAt">The time at which processing started.</param>
    public IdempotencyEntryEntity(
        string scope,
        string key,
        DateTimeOffset startedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        Scope = scope;
        Key = key;
        State = IdempotencyEntryState.Started;
        StartedAt = startedAt;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IdempotencyEntryEntity" /> class for EF Core.
    /// </summary>
    private IdempotencyEntryEntity()
    {
        Scope = null!;
        Key = null!;
    }

    /// <summary>
    /// Gets the operation scope.
    /// </summary>
    public string Scope { get; private set; }

    /// <summary>
    /// Gets the operation key.
    /// </summary>
    public string Key { get; private set; }

    /// <summary>
    /// Gets the operation state.
    /// </summary>
    public IdempotencyEntryState State { get; private set; }

    /// <summary>
    /// Gets when the operation started.
    /// </summary>
    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>
    /// Gets when the operation completed.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>
    /// Gets the optional result fingerprint.
    /// </summary>
    public string? ResultFingerprint { get; private set; }

    /// <summary>
    /// Restarts processing for an expired operation.
    /// </summary>
    /// <param name="startedAt">The new processing start time.</param>
    public void Restart(DateTimeOffset startedAt)
    {
        State = IdempotencyEntryState.Started;
        StartedAt = startedAt;
        CompletedAt = null;
        ResultFingerprint = null;
    }

    /// <summary>
    /// Marks the operation as completed.
    /// </summary>
    /// <param name="completedAt">The time at which processing completed.</param>
    /// <param name="resultFingerprint">The optional stable result fingerprint.</param>
    public void Complete(DateTimeOffset completedAt, string? resultFingerprint)
    {
        State = IdempotencyEntryState.Completed;
        CompletedAt = completedAt;
        ResultFingerprint = resultFingerprint;
    }
}
