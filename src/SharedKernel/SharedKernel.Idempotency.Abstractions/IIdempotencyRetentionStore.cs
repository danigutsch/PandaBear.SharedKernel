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
}
