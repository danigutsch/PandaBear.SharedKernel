namespace PandaBear.Idempotency;

/// <summary>
/// Purges expired idempotency entries while retaining the entries that a hold or a missing retention owner protects.
/// Stores implement it in addition to <see cref="IIdempotencyStore" />; retention periods, the held owner keys and the
/// choice of owner-tracked scopes stay with the host.
/// </summary>
public interface IIdempotencyRetentionStore
{
    /// <summary>
    /// Deletes one bounded batch of expired entries, oldest first, and keeps the entries of held owners and, in owner-tracked
    /// scopes, the entries without an owner.
    /// </summary>
    /// <remarks>
    /// The held owners are fixed before the call. A hold placed after the host computed them is not seen, so a host whose holds
    /// can change while a purge runs uses the overload with a guard, which checks the selected batch itself.
    /// </remarks>
    /// <param name="cutoff">Entries last touched before this time are expired. The last touch is the completion time, or the start time when the entry never completed.</param>
    /// <param name="ownerTrackedScopePrefix">
    /// The prefix of the scopes whose entries must carry a retention owner, or <see langword="null" /> when no scope is owner-tracked.
    /// Inside these scopes an expired entry without an owner is retained and counted as unattributed; outside them it is purged.
    /// </param>
    /// <param name="heldRetentionOwners">The owner keys to keep. An expired entry with one of these owners is not selected and is counted as held, in every scope.</param>
    /// <param name="batchSize">The maximum number of entries to delete; it must be positive.</param>
    /// <param name="ct">A token that can cancel the operation.</param>
    /// <param name="scopePrefix">
    /// Limits the purge to entries whose scope starts with this prefix, or <see langword="null" /> to cover every scope of the table.
    /// Entries outside the prefix are never read, counted or deleted, so a host that shares the table between features
    /// passes the prefix of its own scopes. It cannot be empty or whitespace.
    /// </param>
    /// <returns>The number of entries the batch deleted, plus the expired entries kept because of a held owner or a missing owner.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="heldRetentionOwners" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="batchSize" /> is not positive.</exception>
    /// <exception cref="ArgumentException">A prefix is empty or whitespace, or an owner key is not a valid identifier.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct" /> was cancelled.</exception>
    ValueTask<IdempotencyRetentionPurgeResult> PurgeExpired(
        DateTimeOffset cutoff,
        string? ownerTrackedScopePrefix,
        IReadOnlyCollection<string> heldRetentionOwners,
        int batchSize,
        CancellationToken ct,
        string? scopePrefix = null);

    /// <summary>
    /// Deletes one bounded batch of expired entries like the overload without a guard, and asks the host to confirm the selected
    /// batch first: the guard receives the owners of the selected rows and returns the ones to keep.
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
    /// <param name="heldRetentionOwners">The owner keys to keep. They only pre-filter the batch: an expired entry with one of these owners is not selected and is counted as held, in every scope. The guard decides for the rows that are selected.</param>
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
    /// <returns>The number of entries the batch deleted, plus the expired entries kept because of a held owner, a guard decision or a missing owner.</returns>
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
