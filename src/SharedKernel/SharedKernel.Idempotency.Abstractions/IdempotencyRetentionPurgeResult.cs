namespace PandaBear.Idempotency;

/// <summary>
/// Reports what one retention purge batch did to the expired entries it considered.
/// </summary>
/// <param name="Purged">The number of entries the batch deleted.</param>
/// <param name="Held">The number of expired entries retained because their retention owner is held.</param>
/// <param name="Unattributed">
/// The number of expired entries retained because they have no retention owner inside an owner-tracked scope.
/// </param>
public sealed record IdempotencyRetentionPurgeResult(long Purged, long Held, long Unattributed);
