using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Domain;
using SharedKernel.DomainEvents;

namespace SharedKernel.EntityFrameworkCore;

/// <summary>
/// Dispatches aggregate domain events before EF Core saves changes and clears them after a successful save.
/// </summary>
public sealed class DispatchDomainEventsSaveChangesInterceptor : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        DispatchDomainEvents(eventData.Context, CancellationToken.None).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();

        return result;
    }

    /// <inheritdoc />
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        ClearDomainEvents(eventData.Context);

        return result;
    }

    /// <inheritdoc />
    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        ClearDomainEvents(eventData.Context);

        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);
    }

    /// <inheritdoc />
    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        await DispatchDomainEvents(eventData.Context, cancellationToken).ConfigureAwait(false);

        return result;
    }

    private static async ValueTask DispatchDomainEvents(DbContext? dbContext, CancellationToken ct)
    {
        if (dbContext is null)
        {
            return;
        }

        var domainEvents = GetAggregatesWithDomainEvents(dbContext)
            .SelectMany(static aggregate => aggregate.GetDomainEvents())
            .ToArray();

        var domainEventDispatcher = dbContext.GetService<IDomainEventDispatcher>();

        foreach (var domainEvent in domainEvents)
        {
            await domainEventDispatcher.Dispatch(domainEvent, ct).ConfigureAwait(false);
        }
    }

    private static void ClearDomainEvents(DbContext? dbContext)
    {
        if (dbContext is null)
        {
            return;
        }

        foreach (var aggregate in GetTrackedAggregatesWithDomainEvents(dbContext))
        {
            aggregate.ClearDomainEvents();
        }
    }

    private static EntityEntry<IAggregateRoot>[] GetAggregateEntries(DbContext dbContext) =>
        dbContext.ChangeTracker
            .Entries<IAggregateRoot>()
            .Where(static entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToArray();

    private static IAggregateRoot[] GetAggregatesWithDomainEvents(DbContext dbContext) =>
        GetAggregateEntries(dbContext)
            .Select(static entry => entry.Entity)
            .Where(static aggregate => aggregate.GetDomainEvents().Count > 0)
            .ToArray();

    private static IAggregateRoot[] GetTrackedAggregatesWithDomainEvents(DbContext dbContext) =>
        dbContext.ChangeTracker
            .Entries<IAggregateRoot>()
            .Select(static entry => entry.Entity)
            .Where(static aggregate => aggregate.GetDomainEvents().Count > 0)
            .ToArray();
}
