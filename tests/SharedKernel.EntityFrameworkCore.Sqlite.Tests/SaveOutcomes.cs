using Microsoft.EntityFrameworkCore;
using PandaBear.BuildingBlocks;

namespace PandaBear.EntityFrameworkCore.Sqlite.Tests;

/// <summary>Runs a translating save and returns what it threw, so tests can assert on the outcome.</summary>
public static class SaveOutcomes
{
    /// <summary>Saves and expects a rule violation.</summary>
    /// <param name="context">The context with pending changes.</param>
    /// <param name="parser">The parser to use.</param>
    /// <param name="translateConflicts">Whether to save through the conflict-translating method instead.</param>
    /// <returns>The thrown exception.</returns>
    public static async Task<UniqueConstraintViolationException> ExpectRule(DbContext context, IUniqueViolationParser parser, bool translateConflicts = false)
    {
        try
        {
            await (translateConflicts
                ? context.SaveChangesTranslatingConflicts(parser, TestContext.Current.CancellationToken)
                : context.SaveChangesTranslatingUniqueViolations(parser, TestContext.Current.CancellationToken));
        }
        catch (UniqueConstraintViolationException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("The save did not throw a rule violation.");
    }

    /// <summary>Saves and expects the provider's own exception, untranslated.</summary>
    /// <param name="context">The context with pending changes.</param>
    /// <param name="parser">The parser to use.</param>
    /// <param name="translateConflicts">Whether to save through the conflict-translating method instead.</param>
    /// <returns>The thrown exception.</returns>
    public static async Task<DbUpdateException> ExpectUnchanged(DbContext context, IUniqueViolationParser parser, bool translateConflicts = false)
    {
        try
        {
            await (translateConflicts
                ? context.SaveChangesTranslatingConflicts(parser, TestContext.Current.CancellationToken)
                : context.SaveChangesTranslatingUniqueViolations(parser, TestContext.Current.CancellationToken));
        }
        catch (DbUpdateException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("The save did not throw a provider exception.");
    }
}
