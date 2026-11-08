using Microsoft.EntityFrameworkCore;
using PandaBear.BuildingBlocks;

namespace PandaBear.EntityFrameworkCore.Sqlite.Tests;

/// <summary>Helpers for the conflict-translating save tests.</summary>
public static class ConflictOutcomes
{
    /// <summary>Inserts an account in its own context and returns its key.</summary>
    /// <param name="database">The database.</param>
    /// <returns>The key of the new account.</returns>
    public static async Task<Guid> Seed(SqliteTestDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);

        await using var seed = database.CreateContext();
        var account = new Account();
        seed.Accounts.Add(account);
        await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        return account.Id;
    }

    /// <summary>Deletes an account through another context on the same database.</summary>
    /// <param name="database">The database.</param>
    /// <param name="id">The key of the account.</param>
    /// <returns>A task that completes when the row is gone.</returns>
    public static async Task DeleteElsewhere(SqliteTestDatabase database, Guid id)
    {
        ArgumentNullException.ThrowIfNull(database);

        await using var other = database.CreateContext();
        await other.Accounts.Where(account => account.Id == id).ExecuteDeleteAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Saves and expects a concurrency conflict.</summary>
    /// <param name="context">The context with pending changes.</param>
    /// <param name="parser">The parser to use.</param>
    /// <returns>The thrown exception.</returns>
    public static async Task<ConcurrencyConflictException> ExpectConflict(DbContext context, IUniqueViolationParser parser)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await context.SaveChangesTranslatingConflicts(parser, TestContext.Current.CancellationToken);
        }
        catch (ConcurrencyConflictException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("The save did not throw a concurrency conflict.");
    }
}
