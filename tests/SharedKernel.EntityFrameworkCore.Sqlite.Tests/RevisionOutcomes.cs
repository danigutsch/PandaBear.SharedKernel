using Microsoft.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore.Sqlite.Tests;

/// <summary>Helpers for the revision tests.</summary>
public static class RevisionOutcomes
{
    /// <summary>Creates a context with no provider, for argument checks that never reach the database.</summary>
    /// <returns>A new context.</returns>
    public static NoteDbContext Unused() => new(new DbContextOptions<NoteDbContext>());

    /// <summary>Saves and returns what the save threw.</summary>
    /// <param name="context">The context with pending changes.</param>
    /// <returns>The thrown exception.</returns>
    public static async Task<Exception> FailedSave(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        catch (Exception exception) when (exception is DbUpdateException or Microsoft.Data.Sqlite.SqliteException)
        {
            return exception;
        }

        throw new InvalidOperationException("The save did not throw.");
    }
}
