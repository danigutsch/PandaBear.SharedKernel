namespace PandaBear.Idempotency;

/// <summary>
/// Purges expired idempotency entries while retaining the entries that a hold or a missing retention owner protects.
/// Stores implement it in addition to <see cref="IIdempotencyStore" />; retention periods, the held owner keys and the
/// choice of owner-tracked scopes stay with the host.
/// </summary>
public interface IIdempotencyRetentionStore
{
    /// <summary>
    /// Deletes one bounded batch of entries that are older than the cutoff, oldest first.
    /// </summary>
    /// <param name="cutoff">Entries last touched before this time are expired. The last touch is the completion time, or the start time when the entry never completed.</param>
    /// <param name="ownerTrackedScopePrefix">
    /// The prefix of the scopes whose entries must carry a retention owner, or <see langword="null" /> when no scope is owner-tracked.
    /// Inside these scopes an expired entry without an owner is retained and counted as unattributed; outside them it is purged.
    /// </param>
    /// <param name="heldRetentionOwners">The owner keys that are held. An expired entry with one of these owners is retained and counted as held, in every scope.</param>
    /// <param name="batchSize">The maximum number of entries to delete; it must be positive.</param>
    /// <param name="ct">A token that can cancel the operation.</param>
    /// <param name="scopePrefix">
    /// Limits the purge to entries whose scope starts with this prefix, or <see langword="null" /> to cover every scope of the table.
    /// Entries outside the prefix are never read, counted or deleted, so a host that shares the table between features
    /// passes the prefix of its own scopes. It cannot be empty or whitespace.
    /// </param>
    /// <returns>The purged, held and unattributed counts for the batch.</returns>
    ValueTask<IdempotencyRetentionPurgeResult> PurgeExpired(
        DateTimeOffset cutoff,
        string? ownerTrackedScopePrefix,
        IReadOnlyCollection<string> heldRetentionOwners,
        int batchSize,
        CancellationToken ct,
        string? scopePrefix = null);

    /// <summary>
    /// Deletes one bounded batch of expired entries like the overload without a guard, but lets the host veto owners after the
    /// store has selected the batch and before it deletes anything.
    /// </summary>
    /// <remarks>
    /// The store selects the batch with row locks in the transaction of the caller (or in a transaction of its own when none is open)
    /// and calls <paramref name="guard" /> exactly once with the distinct, non-null retention owners of the selected rows.
    /// It then deletes only the selected rows whose owner the guard did not return. The guard is not called when the batch has no owners.
    /// Entries without an owner in an owner-tracked scope are retained, counted as unattributed and never passed to the guard.
    /// If the guard throws or its token is cancelled, nothing is deleted and the exception propagates unchanged.
    /// <see cref="IdempotencyRetentionPurgeResult.Held" /> counts the expired entries kept because their owner is in
    /// <paramref name="heldRetentionOwners" /> (every such entry of the table or scope prefix, not only the batch) plus the selected
    /// entries kept because the guard returned their owner.
    /// </remarks>
    /// <param name="cutoff">Entries last touched before this time are expired. The last touch is the completion time, or the start time when the entry never completed.</param>
    /// <param name="ownerTrackedScopePrefix">
    /// The prefix of the scopes whose entries must carry a retention owner, or <see langword="null" /> when no scope is owner-tracked.
    /// Inside these scopes an expired entry without an owner is retained and counted as unattributed; outside them it is purged.
    /// </param>
    /// <param name="heldRetentionOwners">The owner keys that are held. They only pre-filter the batch: an expired entry with one of these owners is retained and counted as held, in every scope, and is not selected.</param>
    /// <param name="batchSize">The maximum number of entries to select; it must be positive.</param>
    /// <param name="guard">
    /// Receives the distinct non-null retention owners of the selected batch and returns the subset that must be kept.
    /// It may return <see langword="null" /> or an empty set to keep none, and owners that are not in the batch are ignored.
    /// It runs while the selected rows are locked, so the host checks its holds under its own locks in the same transaction.
    /// </param>
    /// <param name="ct">A token that can cancel the operation. It is also passed to <paramref name="guard" />.</param>
    /// <param name="scopePrefix">
    /// Limits the purge to entries whose scope starts with this prefix, or <see langword="null" /> to cover every scope of the table.
    /// It cannot be empty or whitespace.
    /// </param>
    /// <returns>The purged, held and unattributed counts for the batch.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="heldRetentionOwners" /> or <paramref name="guard" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="batchSize" /> is not positive.</exception>
    /// <exception cref="ArgumentException">A prefix is empty or whitespace, or an owner key is not a valid identifier.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct" /> was cancelled.</exception>
    ValueTask<IdempotencyRetentionPurgeResult> PurgeExpired(
        DateTimeOffset cutoff,
        string? ownerTrackedScopePrefix,
        IReadOnlyCollection<string> heldRetentionOwners,
        int batchSize,
        Func<IReadOnlyCollection<string>, CancellationToken, ValueTask<IReadOnlySet<string>>> guard,
        CancellationToken ct,
        string? scopePrefix = null);
}
