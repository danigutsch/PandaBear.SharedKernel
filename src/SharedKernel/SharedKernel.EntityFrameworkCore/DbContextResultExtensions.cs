using Microsoft.EntityFrameworkCore;
using SharedKernel.BuildingBlocks;
using SharedKernel.Results;

namespace SharedKernel.EntityFrameworkCore;

/// <summary>Saves changes and reports a lost race as a <see cref="Result"/> instead of an exception.</summary>
public static class DbContextResultExtensions
{
    /// <summary>
    /// The fixed, data-free detail of the conflict result for a row that changed or disappeared since it was read. It is the same text as
    /// the mediator's concurrency conflict behavior, so a stale form and a lost race look identical to the caller.
    /// </summary>
    public const string ConcurrencyConflictMessage = "The data was changed or removed by someone else. Reload it and try again.";

    /// <summary>
    /// Saves all changes like <see cref="DbContextUniqueViolationExtensions.SaveChangesTranslatingConflicts"/>, but returns the outcome of a
    /// lost race instead of throwing it, for code that returns a <see cref="Result"/> and does not run through a mediator pipeline:
    /// <list type="bullet">
    /// <item><description>A successful save returns <see cref="Result.Ok()"/>.</description></item>
    /// <item><description>
    /// A violation of a unique index tagged with <see cref="UniqueRuleIndexBuilderExtensions.HasUniqueRule{TEntity}"/> returns what
    /// <paramref name="uniqueRuleResult"/> makes of the rule, typically the same field error as the caller's pre-check.
    /// </description></item>
    /// <item><description>
    /// A row that changed or disappeared since it was read returns a <see cref="ResultStatus.Conflict"/> result with the code
    /// <see cref="ResultErrorCodes.ConcurrencyConflict"/> and <see cref="ConcurrencyConflictMessage"/>.
    /// </description></item>
    /// </list>
    /// After a reported violation or conflict the change tracker is cleared, so the context can be used again without repeating the failed
    /// write. Every other failure, including violations of untagged indexes and cancellation, propagates unchanged and leaves the tracker as
    /// it was.
    /// </summary>
    /// <param name="dbContext">The context.</param>
    /// <param name="parser">The provider's violation parser.</param>
    /// <param name="uniqueRuleResult">Makes the result of a violated rule; it should be a failure.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A success, the mapped result of a violated rule, or a concurrency conflict.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async Task<Result> SaveChangesAsResult(
        this DbContext dbContext,
        IUniqueViolationParser parser,
        Func<string, Result> uniqueRuleResult,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(uniqueRuleResult);

        try
        {
            await dbContext.SaveChangesTranslatingConflicts(parser, ct).ConfigureAwait(false);
            return Result.Ok();
        }
        catch (UniqueConstraintViolationException exception)
        {
            dbContext.ChangeTracker.Clear();
            return uniqueRuleResult(exception.Rule);
        }
        catch (ConcurrencyConflictException)
        {
            dbContext.ChangeTracker.Clear();
            return Result.FromError(new ResultError(ConcurrencyConflictMessage, ResultErrorCodes.ConcurrencyConflict));
        }
    }
}
