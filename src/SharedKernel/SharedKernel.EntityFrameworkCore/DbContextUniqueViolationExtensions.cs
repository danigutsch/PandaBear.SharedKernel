using Microsoft.EntityFrameworkCore;
using PandaBear.BuildingBlocks;

namespace PandaBear.EntityFrameworkCore;

/// <summary>Saves changes and reports unique-index violations by application rule, and optionally concurrency conflicts.</summary>
public static class DbContextUniqueViolationExtensions
{
    /// <summary>
    /// Saves all changes. A violation of a unique index tagged with <see cref="UniqueRuleIndexBuilderExtensions.HasUniqueRule{TEntity}"/>
    /// becomes a <see cref="UniqueConstraintViolationException"/> carrying the rule; every other failure, including violations of
    /// untagged indexes, propagates unchanged.
    /// </summary>
    /// <param name="dbContext">The context.</param>
    /// <param name="parser">The provider's violation parser.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The number of state entries written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dbContext"/> or <paramref name="parser"/> is <see langword="null"/>.</exception>
    /// <exception cref="UniqueConstraintViolationException">A tagged unique index was violated.</exception>
    public static async Task<int> SaveChangesTranslatingUniqueViolations(this DbContext dbContext, IUniqueViolationParser parser, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(parser);

        try
        {
            return await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (TryGetRule(dbContext, parser, exception, out var rule))
        {
            throw new UniqueConstraintViolationException(rule, exception);
        }
    }

    /// <summary>
    /// Saves all changes like <see cref="SaveChangesTranslatingUniqueViolations"/>, and additionally reports a row that changed or
    /// disappeared since it was read: a <see cref="DbUpdateConcurrencyException"/> becomes a
    /// <see cref="ConcurrencyConflictException"/> with the provider exception kept as the inner exception. Unique violations of tagged
    /// indexes become <see cref="UniqueConstraintViolationException"/>; every other failure propagates unchanged.
    /// </summary>
    /// <param name="dbContext">The context.</param>
    /// <param name="parser">The provider's violation parser.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The number of state entries written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dbContext"/> or <paramref name="parser"/> is <see langword="null"/>.</exception>
    /// <exception cref="UniqueConstraintViolationException">A tagged unique index was violated.</exception>
    /// <exception cref="ConcurrencyConflictException">A row changed or disappeared since it was read.</exception>
    public static async Task<int> SaveChangesTranslatingConflicts(this DbContext dbContext, IUniqueViolationParser parser, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(parser);

        try
        {
            return await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(exception);
        }
        catch (DbUpdateException exception) when (TryGetRule(dbContext, parser, exception, out var rule))
        {
            throw new UniqueConstraintViolationException(rule, exception);
        }
    }

    private static bool TryGetRule(DbContext dbContext, IUniqueViolationParser parser, DbUpdateException exception, out string rule)
    {
        rule = string.Empty;
        if (!parser.TryParse(exception, out var violation))
        {
            return false;
        }

        var resolved = UniqueRuleResolver.Resolve(dbContext.Model, violation);
        if (resolved is null)
        {
            return false;
        }

        rule = resolved;
        return true;
    }
}
