using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.EntityFrameworkCore;

namespace SharedKernel.Domain.EntityFrameworkCore;

/// <summary>
/// Dispatches aggregate domain events before EF Core saves changes and clears them after a successful save.
/// </summary>
internal sealed class DispatchDomainEventsSaveChangesInterceptor : SaveChangesInterceptor
{
    private static readonly ConditionalWeakTable<DbContext, DispatchedEventState> DispatchedEvents = new();

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        throw new InvalidOperationException("Synchronous SaveChanges is not supported; use SaveChangesAsync.");
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
            .Where(domainEvent => !HasDispatched(dbContext, domainEvent))
            .ToArray();

        if (domainEvents.Length == 0)
        {
            return;
        }

        using var currentDbContext = CurrentSaveChangesDbContext.Enter(dbContext);
        var applicationServiceProvider = dbContext.GetService<IDbContextOptions>()
            .FindExtension<CoreOptionsExtension>()?
            .ApplicationServiceProvider
            ?? throw new InvalidOperationException("The DbContext application service provider is unavailable.");
        var dispatchScope = applicationServiceProvider.CreateAsyncScope();
        await using var configuredDispatchScope = dispatchScope.ConfigureAwait(false);
        var domainEventDispatcher = dispatchScope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();

        foreach (var domainEvent in domainEvents)
        {
            await domainEventDispatcher.Dispatch(domainEvent, ct).ConfigureAwait(false);
            MarkDispatched(dbContext, domainEvent);
        }
    }

    private static bool HasDispatched(DbContext dbContext, IDomainEvent domainEvent) =>
        TryGetCurrentDispatchState(dbContext, out var state) && state.Events.Contains(domainEvent);

    private static void MarkDispatched(DbContext dbContext, IDomainEvent domainEvent)
    {
        var state = GetCurrentDispatchState(dbContext);
        _ = state.Events.Add(domainEvent);
    }

    private static DispatchedEventState GetCurrentDispatchState(DbContext dbContext)
    {
        if (TryGetCurrentDispatchState(dbContext, out var state))
        {
            return state;
        }

        state = new DispatchedEventState(dbContext.ContextId);
        DispatchedEvents.Add(dbContext, state);

        return state;
    }

    private static bool TryGetCurrentDispatchState(DbContext dbContext, out DispatchedEventState state)
    {
        if (DispatchedEvents.TryGetValue(dbContext, out state!) && state.ContextId == dbContext.ContextId)
        {
            return true;
        }

        _ = DispatchedEvents.Remove(dbContext);
        state = null!;
        return false;
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

        DispatchedEvents.Remove(dbContext);
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

    private sealed class DispatchedEventState(DbContextId contextId)
    {
        public DbContextId ContextId { get; } = contextId;

        public HashSet<IDomainEvent> Events { get; } = new(ReferenceEqualityComparer.Instance);
    }
}
