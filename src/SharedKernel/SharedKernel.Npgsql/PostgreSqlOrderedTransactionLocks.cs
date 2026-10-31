using Npgsql;

namespace PandaBear.Npgsql;

/// <summary>
/// Takes transaction-scoped PostgreSQL advisory locks in one global order, so transactions that need several locks cannot deadlock on them.
/// </summary>
/// <remarks>
/// <para>
/// An instance is bound to one open transaction and never begins, commits, rolls back or disposes it. The instance remembers the locks it
/// took, so create exactly one instance per transaction attempt and share it with every piece of code that takes locks in that transaction,
/// for example by keeping it in the unit of work. Separate instances over the same transaction do not know each other's locks and cannot
/// check the order across them. Do not reuse an instance after its transaction completes, including for a retry: Npgsql reuses its
/// <see cref="NpgsqlTransaction" /> object for the next transaction on a connection, so a new attempt needs a new instance.
/// </para>
/// <para>
/// The order is the tier, then the numeric key from <see cref="PostgreSqlAdvisoryLockKey.FromUtf8Sha256BigEndian(string)" />, then the
/// ordinal name. Locks requested together are deduplicated by key, keeping the lowest tier, and taken one statement at a time in that order.
/// Every requested lock is taken again even when this instance took it before, because PostgreSQL releases a lock taken after a savepoint
/// when the transaction rolls back to that savepoint; taking a lock the transaction still holds is harmless. A lock this instance took
/// before is exempt from the order check. A request that would take a lock sorting before one already taken, whether in a lower
/// tier or earlier within the same tier, throws before any lock of that request is taken: taking it could deadlock against a transaction
/// that takes the same locks in the order this type enforces. Request all locks of a tier together when possible. After rolling back to a
/// savepoint that was created before locks were taken, treat those locks as released: use a new instance if the original order must be
/// enforced again.
/// </para>
/// <para>
/// Instances are not thread-safe, like the connection and transaction they use.
/// </para>
/// </remarks>
public sealed class PostgreSqlOrderedTransactionLocks
{
    private readonly NpgsqlConnection connection;
    private readonly NpgsqlTransaction transaction;
    private readonly HashSet<PostgreSqlLockKey> heldKeys = [];
    private OrderedLock? lastTaken;

    /// <summary>
    /// Creates a helper for the given open transaction.
    /// </summary>
    /// <param name="connection">The open PostgreSQL connection that owns <paramref name="transaction" />.</param>
    /// <param name="transaction">The active PostgreSQL transaction that owns the advisory locks.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connection" /> or <paramref name="transaction" /> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="transaction" /> is not an open transaction of <paramref name="connection" />.</exception>
    public PostgreSqlOrderedTransactionLocks(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction must be an open transaction of the connection.", nameof(transaction));
        }

        this.connection = connection;
        this.transaction = transaction;
    }

    /// <summary>
    /// Takes the requested locks that the transaction does not hold yet, in tier, key and name order.
    /// </summary>
    /// <param name="locks">The locks to take. An empty sequence takes nothing.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when PostgreSQL has granted every requested lock.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="locks" /> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A request has a negative tier.</exception>
    /// <exception cref="ArgumentException">A request has a null or empty name.</exception>
    /// <exception cref="InvalidOperationException">
    /// The transaction is no longer open, or a requested lock sorts before a lock the transaction already took.
    /// </exception>
    /// <remarks>
    /// Validation and the order check run before the first lock is taken, so a rejected request takes no lock. If acquisition fails or is
    /// cancelled part way, the locks already granted stay held and are remembered; PostgreSQL releases them with the transaction.
    /// </remarks>
    public async ValueTask Acquire(IEnumerable<PostgreSqlTransactionLockRequest> locks, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(locks);

        if (transaction.Connection is null)
        {
            throw new InvalidOperationException("The transaction is no longer open.");
        }

        var pending = Order(locks);
        if (pending.Length == 0)
        {
            return;
        }

        var firstNew = Array.Find(pending, candidate => !candidate.AlreadyTaken);
        if (firstNew.Name is not null && lastTaken is { } taken && firstNew.CompareTo(taken) < 0)
        {
            throw new InvalidOperationException(
                $"Lock '{firstNew.Name}' (tier {firstNew.Tier}) cannot be taken after lock '{taken.Name}' (tier {taken.Tier}); "
                + "locks must be taken in tier, key, name order within one transaction.");
        }

        foreach (var next in pending)
        {
            await PostgreSqlTransactionAdvisoryLock.AcquireKey(connection, transaction, next.Key, ct);
            if (heldKeys.Add(next.Key) && (lastTaken is null || next.CompareTo(lastTaken.Value) > 0))
            {
                lastTaken = next;
            }
        }
    }

    private OrderedLock[] Order(IEnumerable<PostgreSqlTransactionLockRequest> locks)
    {
        var lowestPerKey = new Dictionary<PostgreSqlLockKey, OrderedLock>();
        foreach (var request in locks)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(request.Tier);
            ArgumentException.ThrowIfNullOrEmpty(request.Name);

            var key = PostgreSqlLockKey.FromIdentity(request.Name);
            var candidate = new OrderedLock(request.Tier, key, request.Name, heldKeys.Contains(key));

            if (!lowestPerKey.TryGetValue(candidate.Key, out var existing) || candidate.CompareTo(existing) < 0)
            {
                lowestPerKey[candidate.Key] = candidate;
            }
        }

        var ordered = new OrderedLock[lowestPerKey.Count];
        lowestPerKey.Values.CopyTo(ordered, 0);
        Array.Sort(ordered);
        return ordered;
    }

    private readonly record struct OrderedLock(int Tier, PostgreSqlLockKey Key, string Name, bool AlreadyTaken) : IComparable<OrderedLock>
    {
        public int CompareTo(OrderedLock other)
        {
            var byTier = Tier.CompareTo(other.Tier);
            if (byTier != 0)
            {
                return byTier;
            }

            var byKey = Key.CompareTo(other.Key);
            return byKey != 0 ? byKey : string.CompareOrdinal(Name, other.Name);
        }
    }
}
