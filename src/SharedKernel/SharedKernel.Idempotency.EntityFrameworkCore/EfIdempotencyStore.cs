using Microsoft.EntityFrameworkCore;

namespace SharedKernel.Idempotency.EntityFrameworkCore;

/// <summary>
/// Stores idempotency entries in EF Core.
/// </summary>
/// <typeparam name="TContext">The DbContext type that owns the idempotency table.</typeparam>
internal sealed class EfIdempotencyStore<TContext>(TContext dbContext) : IIdempotencyStore
    where TContext : DbContext
{
    /// <inheritdoc />
    public async ValueTask<IdempotencyStartResult> TryStart(
        IdempotencyOperation operation,
        DateTimeOffset startedAt,
        TimeSpan? lockDuration,
        CancellationToken ct)
    {
        var existing = await Find(operation, ct).ConfigureAwait(false);
        if (existing is null)
        {
            var entry = new IdempotencyEntryEntity(
                operation.Scope.Value,
                operation.Key.Value,
                startedAt);

            dbContext.Set<IdempotencyEntryEntity>().Add(entry);
            try
            {
                await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            catch (DbUpdateException)
            {
                var concurrentEntry = await FindStartedByConcurrentCaller(operation, entry, ct).ConfigureAwait(false);
                if (concurrentEntry is null)
                {
                    throw;
                }

                return IdempotencyStartResult.AlreadyStarted(ToEntry(concurrentEntry));
            }

            return IdempotencyStartResult.StartedNew();
        }

        if (existing.State is IdempotencyEntryState.Completed || !IsExpired(existing, startedAt, lockDuration))
        {
            return IdempotencyStartResult.AlreadyStarted(ToEntry(existing));
        }

        existing.Restart(startedAt);

        try
        {
            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            var concurrentEntry = await Find(operation, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Idempotency operation could not be restarted because it no longer exists.");

            return IdempotencyStartResult.AlreadyStarted(ToEntry(concurrentEntry));
        }

        return IdempotencyStartResult.StartedNew();
    }

    /// <inheritdoc />
    public async ValueTask Complete(
        IdempotencyOperation operation,
        DateTimeOffset completedAt,
        string? resultFingerprint,
        CancellationToken ct)
    {
        var existing = await Find(operation, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Idempotency operation must be started before it can be completed.");

        existing.Complete(completedAt, resultFingerprint);
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IdempotencyEntry?> Get(IdempotencyOperation operation, CancellationToken ct)
    {
        var existing = await Find(operation, ct).ConfigureAwait(false);

        return existing is null ? null : ToEntry(existing);
    }

    private ValueTask<IdempotencyEntryEntity?> Find(IdempotencyOperation operation, CancellationToken ct) =>
        dbContext.Set<IdempotencyEntryEntity>().FindAsync([operation.Scope.Value, operation.Key.Value], ct);

    private async ValueTask<IdempotencyEntryEntity?> FindStartedByConcurrentCaller(
        IdempotencyOperation operation,
        IdempotencyEntryEntity entry,
        CancellationToken ct)
    {
        dbContext.Entry(entry).State = EntityState.Detached;
        return await Find(operation, ct).ConfigureAwait(false);
    }

    private static bool IsExpired(
        IdempotencyEntryEntity existing,
        DateTimeOffset startedAt,
        TimeSpan? lockDuration) =>
        existing.State is IdempotencyEntryState.Started
        && lockDuration is not null
        && existing.StartedAt.Add(lockDuration.Value) <= startedAt;

    private static IdempotencyEntry ToEntry(IdempotencyEntryEntity entity) =>
        new(
            new IdempotencyOperation(
                IdempotencyScope.From(entity.Scope),
                IdempotencyKey.From(entity.Key)),
            entity.State,
            entity.StartedAt,
            entity.CompletedAt,
            entity.ResultFingerprint);
}
