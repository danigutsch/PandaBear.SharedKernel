using Microsoft.EntityFrameworkCore;
using PandaBear.Npgsql;

namespace PandaBear.DispositionHold.EntityFrameworkCore;

/// <summary>
/// Serializes placing a hold against checking for one, per scope, with PostgreSQL transaction-scoped advisory locks
/// that release at commit or rollback. This is the last lock tier: a transaction takes every lock its command
/// declares first and hold locks afterwards, and never asks for a command lock after a hold lock, so hold locks cannot
/// take part in a lock cycle with them. Within the tier, locks are taken in ascending key order, then name order, so
/// two transactions over overlapping scopes cannot cross. Taking a lock this transaction already holds changes nothing.
/// The transaction belongs to the caller; without one the lock would release at once and protect nothing. It must run
/// at read committed: each statement then takes a new snapshot, so the query after the lock sees a hold committed while
/// the lock was awaited. Repeatable read and serializable take their snapshot at the first statement, which can predate
/// that commit, so they are refused.
/// </summary>
internal static class HoldScopeLock
{
    private const string ReadCommittedIsolation = "read committed";
    private const string NpgsqlProviderName = "Npgsql.EntityFrameworkCore.PostgreSQL";

    /// <summary>Takes the lock of every scope, in a fixed order, inside the context's open transaction.</summary>
    /// <param name="dbContext">The context whose open transaction scopes the locks.</param>
    /// <param name="scopes">The scopes to lock.</param>
    /// <param name="ct">A token that can cancel the operation.</param>
    /// <exception cref="DispositionHoldStateUnknownException">Thrown when there is no open transaction, the provider is not PostgreSQL, or the transaction is not read committed.</exception>
    public static async ValueTask Acquire(DbContext dbContext, IEnumerable<DispositionScope> scopes, CancellationToken ct)
    {
        if (dbContext.Database.ProviderName != NpgsqlProviderName)
        {
            throw new DispositionHoldStateUnknownException(
                "Disposition hold locking supports PostgreSQL only; hold state cannot be serialized on this provider.");
        }

        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new DispositionHoldStateUnknownException(
                "Disposition hold state can only be checked or changed inside an open transaction.");
        }

        var isolation = await dbContext.Database
            .SqlQuery<string>($"SELECT current_setting('transaction_isolation') AS \"Value\"")
            .SingleAsync(ct)
            .ConfigureAwait(false);
        if (isolation != ReadCommittedIsolation)
        {
            throw new DispositionHoldStateUnknownException(
                $"Disposition hold state needs a {ReadCommittedIsolation} transaction; this one is {isolation}, whose snapshot can predate a hold committed while the lock was awaited.");
        }

        var ordered = scopes
            .Select(scope => LockName(scope))
            .Select(name => (Name: name, Key: PostgreSqlAdvisoryLockKey.FromUtf8Sha256BigEndian(name)))
            .DistinctBy(item => item.Key)
            .OrderBy(item => item.Key)
            .ThenBy(item => item.Name, StringComparer.Ordinal);
        foreach (var (_, key) in ordered)
        {
            await dbContext.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({key})", ct).ConfigureAwait(false);
        }
    }

    private static string LockName(DispositionScope scope) => $"disposition-hold:{scope}";
}
