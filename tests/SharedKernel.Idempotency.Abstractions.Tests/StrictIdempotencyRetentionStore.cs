namespace SharedKernel.Idempotency.Tests;

/// <summary>Represents an existing retention implementation with only the strict overloads.</summary>
internal sealed class StrictIdempotencyRetentionStore : IIdempotencyRetentionStore
{
    /// <inheritdoc />
    public ValueTask<IdempotencyRetentionPurgeResult> PurgeExpired(
        DateTimeOffset cutoff,
        string? ownerTrackedScopePrefix,
        IReadOnlyCollection<string> heldRetentionOwners,
        int batchSize,
        CancellationToken ct,
        string? scopePrefix = null) => ValueTask.FromResult(new IdempotencyRetentionPurgeResult(1, 0, 0));

    /// <inheritdoc />
    public ValueTask<IdempotencyRetentionPurgeResult> PurgeExpired(
        DateTimeOffset cutoff,
        string? ownerTrackedScopePrefix,
        IReadOnlyCollection<string> heldRetentionOwners,
        int batchSize,
        Func<IReadOnlyCollection<string>, CancellationToken, ValueTask<IReadOnlySet<string>>> guard,
        CancellationToken ct,
        string? scopePrefix = null) => PurgeExpired(cutoff, ownerTrackedScopePrefix, heldRetentionOwners, batchSize, ct, scopePrefix);
}
