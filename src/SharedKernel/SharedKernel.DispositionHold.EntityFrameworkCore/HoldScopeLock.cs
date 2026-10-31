using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using PandaBear.Npgsql;

namespace PandaBear.DispositionHold.EntityFrameworkCore;

/// <summary>
/// Serializes placing a hold against checking for one, per scope, with PostgreSQL transaction-scoped advisory locks
/// that release at commit or rollback. Hold locks are the last lock tier: a transaction takes every lock its command
/// declares first and hold locks afterwards, and never asks for a command lock after a hold lock, so hold locks cannot
/// take part in a lock cycle with them. The locks go through <see cref="PostgreSqlOrderedTransactionLocks" />, which
/// takes a set in key order and refuses a later request that sorts before one already taken in the same transaction.
/// The transaction belongs to the caller; without one the lock would release at once and protect nothing. It must run
/// at read committed: each statement then takes a new snapshot, so the query after the lock sees a hold committed while
/// the lock was awaited. Repeatable read and serializable take their snapshot at the first statement, which can predate
/// that commit, so they are refused.
/// </summary>
/// <param name="dbContext">The context whose open transaction scopes the locks.</param>
internal sealed class HoldScopeLock(DbContext dbContext)
{
    private const int HoldTier = 1;
    private const string ReadCommittedIsolation = "read committed";
    private const string NpgsqlProviderName = "Npgsql.EntityFrameworkCore.PostgreSQL";

    private Guid sessionTransactionId;
    private PostgreSqlOrderedTransactionLocks? session;

    /// <summary>Returns the advisory lock name of a scope.</summary>
    /// <param name="scope">The scope to lock.</param>
    /// <returns>The lock name.</returns>
    public static string LockName(DispositionScope scope) => $"disposition-hold:{scope}";

    /// <summary>Takes the lock of every scope, in a fixed order, inside the context's open transaction.</summary>
    /// <param name="scopes">The scopes to lock.</param>
    /// <param name="ct">A token that can cancel the operation.</param>
    /// <exception cref="DispositionHoldStateUnknownException">Thrown when there is no open transaction, the provider is not PostgreSQL, or the transaction is not read committed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a request sorts before a hold lock this transaction already took.</exception>
    public async ValueTask Acquire(IEnumerable<DispositionScope> scopes, CancellationToken ct)
    {
        if (dbContext.Database.ProviderName != NpgsqlProviderName)
        {
            throw new DispositionHoldStateUnknownException(
                "Disposition hold locking supports PostgreSQL only; hold state cannot be serialized on this provider.");
        }

        var transaction = dbContext.Database.CurrentTransaction
            ?? throw new DispositionHoldStateUnknownException(
                "Disposition hold state can only be checked or changed inside an open transaction.");

        var isolation = await dbContext.Database
            .SqlQuery<string>($"SELECT current_setting('transaction_isolation') AS \"Value\"")
            .SingleAsync(ct)
            .ConfigureAwait(false);
        if (isolation != ReadCommittedIsolation)
        {
            throw new DispositionHoldStateUnknownException(
                $"Disposition hold state needs a {ReadCommittedIsolation} transaction; this one is {isolation}, whose snapshot can predate a hold committed while the lock was awaited.");
        }

        if (session is null || sessionTransactionId != transaction.TransactionId)
        {
            session = new PostgreSqlOrderedTransactionLocks(
                (NpgsqlConnection)dbContext.Database.GetDbConnection(),
                (NpgsqlTransaction)transaction.GetDbTransaction());
            sessionTransactionId = transaction.TransactionId;
        }

        await session.Acquire(scopes.Select(scope => new PostgreSqlTransactionLockRequest(HoldTier, LockName(scope))), ct)
            .ConfigureAwait(false);
    }
}
