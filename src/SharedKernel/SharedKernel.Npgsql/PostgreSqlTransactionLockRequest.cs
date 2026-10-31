namespace PandaBear.Npgsql;

/// <summary>
/// Names one transaction-scoped advisory lock and the tier that fixes where it sorts relative to other locks.
/// </summary>
/// <param name="Tier">The non-negative tier. Lower tiers are taken before higher tiers.</param>
/// <param name="Name">The caller-composed lock identity passed to <see cref="PostgreSqlAdvisoryLockKey.FromUtf8Sha256BigEndian(string)" />.</param>
public readonly record struct PostgreSqlTransactionLockRequest(int Tier, string Name);
