using Microsoft.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore;

/// <summary>
/// Runs continuations inside retry-compatible EF Core transactions.
/// </summary>
public static class EfCoreCommandTransactionScope
{
    /// <summary>
    /// Executes the next step inside the active EF Core execution strategy and transaction.
    /// </summary>
    /// <param name="dbContext">The DbContext that owns the transaction boundary.</param>
    /// <param name="next">The next operation.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <typeparam name="TResponse">The operation response type.</typeparam>
    /// <returns>The operation response.</returns>
    public static async ValueTask<TResponse> Execute<TResponse>(
        DbContext dbContext,
        Func<ValueTask<TResponse>> next,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(next);

        if (!dbContext.Database.IsRelational())
        {
            return await next().ConfigureAwait(false);
        }

        if (dbContext.Database.CurrentTransaction is not null)
        {
            return await next().ConfigureAwait(false);
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync((dbContext, next), ExecuteInTransaction, ct).ConfigureAwait(false);
    }

    private static async Task<TResponse> ExecuteInTransaction<TResponse>(
        (DbContext DbContext, Func<ValueTask<TResponse>> Next) state,
        CancellationToken ct)
    {
        var transaction = await state.DbContext.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        await using var _ = transaction.ConfigureAwait(false);
        var response = await state.Next().ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);

        return response;
    }
}
