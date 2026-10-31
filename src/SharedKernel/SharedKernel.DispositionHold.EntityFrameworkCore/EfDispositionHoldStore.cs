using PandaBear.BuildingBlocks;
using Microsoft.EntityFrameworkCore;

namespace PandaBear.DispositionHold.EntityFrameworkCore;

/// <summary>
/// Stores holds and answers hold checks in one DbContext. Both operations lock the affected scopes first, so a hold
/// placed while a purge checks is either seen by the purge or waits for it to finish. Any failure to determine hold
/// state surfaces as <see cref="DispositionHoldStateUnknownException" />, never as a clear decision.
/// </summary>
/// <typeparam name="TContext">The DbContext type that owns the hold table.</typeparam>
internal sealed class EfDispositionHoldStore<TContext>(TContext dbContext) : IDispositionHoldStore, IDispositionHoldGuard
    where TContext : DbContext
{
    /// <inheritdoc />
    public async ValueTask Add(Hold hold, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(hold);

        await HoldScopeLock.Acquire(dbContext, [hold.Scope], ct).ConfigureAwait(false);
        dbContext.Set<Hold>().Add(hold);
    }

    /// <inheritdoc />
    public async ValueTask<Hold?> Find(Guid id, CancellationToken ct) =>
        await dbContext.Set<Hold>().FirstOrDefaultAsync(hold => hold.Id == id, ct).ConfigureAwait(false);

    /// <inheritdoc />
    public async ValueTask<DispositionHoldDecision> Check(
        IReadOnlyCollection<DispositionScope> scopes,
        DateTimeOffset at,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        if (scopes.Count == 0)
        {
            return DispositionHoldDecision.Clear;
        }

        try
        {
            await HoldScopeLock.Acquire(dbContext, scopes, ct).ConfigureAwait(false);
            var keys = scopes.ToArray();
            var holdIds = await dbContext.Set<Hold>()
                .AsNoTracking()
                .Where(hold => keys.Contains(hold.Scope)
                    && hold.ReleasedAt == null
                    && hold.EffectiveFrom <= at
                    && (hold.ExpiresAt == null || at < hold.ExpiresAt))
                .Select(hold => hold.Id)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            return holdIds.Count == 0 ? DispositionHoldDecision.Clear : DispositionHoldDecision.HeldBy(holdIds);
        }
        catch (Exception exception) when (exception is not DispositionHoldStateUnknownException && exception.ShouldHandleAsFailure(ct))
        {
            throw new DispositionHoldStateUnknownException("Disposition hold state could not be determined.", exception);
        }
    }
}
