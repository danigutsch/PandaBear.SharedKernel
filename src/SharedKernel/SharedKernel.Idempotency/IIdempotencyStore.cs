namespace SharedKernel.Idempotency;

/// <summary>
/// Defines storage-neutral idempotency operations.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Attempts to start processing an idempotent operation.
    /// </summary>
    /// <param name="operation">The idempotent operation.</param>
    /// <param name="startedAt">The time at which processing starts.</param>
    /// <param name="lockDuration">The optional processing lock duration.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>A result indicating whether processing ownership was acquired.</returns>
    ValueTask<IdempotencyStartResult> TryStart(
        IdempotencyOperation operation,
        DateTimeOffset startedAt,
        TimeSpan? lockDuration,
        CancellationToken cancellationToken);

    /// <summary>
    /// Marks an idempotent operation as completed.
    /// </summary>
    /// <param name="operation">The idempotent operation.</param>
    /// <param name="completedAt">The time at which processing completed.</param>
    /// <param name="resultFingerprint">An optional stable fingerprint of the completed result.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>A task that completes when the entry has been updated.</returns>
    ValueTask Complete(
        IdempotencyOperation operation,
        DateTimeOffset completedAt,
        string? resultFingerprint,
        CancellationToken cancellationToken);

    /// <summary>
    /// Marks an idempotent operation as completed without recording a result fingerprint.
    /// </summary>
    /// <param name="operation">The idempotent operation.</param>
    /// <param name="completedAt">The time at which processing completed.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>A task that completes when the entry has been updated.</returns>
    ValueTask Complete(
        IdempotencyOperation operation,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken) => Complete(
            operation,
            completedAt,
            resultFingerprint: null,
            cancellationToken: cancellationToken);

    /// <summary>
    /// Gets the stored idempotency entry for an operation, when one exists.
    /// </summary>
    /// <param name="operation">The idempotent operation.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The stored entry, or <see langword="null" /> when none exists.</returns>
    ValueTask<IdempotencyEntry?> Get(IdempotencyOperation operation, CancellationToken cancellationToken);
}
