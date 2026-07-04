using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Domain;
using SharedKernel.DomainEvents;

namespace SharedKernel.EntityFrameworkCore;

/// <summary>
/// Dispatches aggregate domain events before EF Core saves changes and clears them after a successful save.
/// </summary>
public sealed class DispatchDomainEventsSaveChangesInterceptor(
    IServiceProvider serviceProvider) : SaveChangesInterceptor
{
    private readonly List<IAggregateRoot> aggregatesToClear = [];

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

        ClearDomainEvents();

        return result;
    }

    /// <inheritdoc />
    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        ClearDomainEvents();

        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        ForgetTrackedAggregates();
    }

    /// <inheritdoc />
    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        ForgetTrackedAggregates();

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

    private async ValueTask DispatchDomainEvents(DbContext? dbContext, CancellationToken ct)
    {
        if (dbContext is null)
        {
            return;
        }

        var aggregates = GetAggregateEntries(dbContext)
            .Select(static entry => entry.Entity)
            .Where(static aggregate => aggregate.GetDomainEvents().Count > 0)
            .ToArray();

        aggregatesToClear.Clear();
        aggregatesToClear.AddRange(aggregates);

        var domainEvents = aggregates
            .SelectMany(static aggregate => aggregate.GetDomainEvents())
            .ToArray();

        var domainEventDispatcher = serviceProvider.GetRequiredService<IDomainEventDispatcher>();

        foreach (var domainEvent in domainEvents)
        {
            await domainEventDispatcher.Dispatch(domainEvent, ct).ConfigureAwait(false);
        }
    }

    private void ClearDomainEvents()
    {
        foreach (var aggregate in aggregatesToClear)
        {
            aggregate.ClearDomainEvents();
        }

        aggregatesToClear.Clear();
    }

    private void ForgetTrackedAggregates()
    {
        aggregatesToClear.Clear();
    }

    private static EntityEntry<IAggregateRoot>[] GetAggregateEntries(DbContext dbContext) =>
        dbContext.ChangeTracker
            .Entries<IAggregateRoot>()
            .Where(static entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToArray();
}
