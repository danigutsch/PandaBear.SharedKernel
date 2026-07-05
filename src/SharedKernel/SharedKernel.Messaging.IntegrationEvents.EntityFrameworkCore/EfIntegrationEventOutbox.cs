using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.EntityFrameworkCore;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Stores integration events in the current EF Core unit of work.
/// </summary>
/// <typeparam name="TContext">The DbContext type that owns the outbox table.</typeparam>
internal sealed class EfIntegrationEventOutbox<TContext> : IIntegrationEventOutbox
    where TContext : DbContext
{
    private readonly TContext? dbContext;
    private readonly TimeProvider timeProvider;
    private readonly IIntegrationEventSerializer serializer;

    [ActivatorUtilitiesConstructor]
    public EfIntegrationEventOutbox(
        TimeProvider timeProvider,
        IIntegrationEventSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(serializer);

        this.timeProvider = timeProvider;
        this.serializer = serializer;
    }

    internal EfIntegrationEventOutbox(
        TContext dbContext,
        TimeProvider timeProvider,
        IIntegrationEventSerializer serializer)
        : this(timeProvider, serializer)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        this.dbContext = dbContext;
    }

    /// <inheritdoc />
    public ValueTask Enqueue<TIntegrationEvent>(TIntegrationEvent integrationEvent, CancellationToken ct)
        where TIntegrationEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ct.ThrowIfCancellationRequested();

        var envelope = new EventEnvelope(
            integrationEvent.EventId,
            TIntegrationEvent.EventType,
            TIntegrationEvent.EventVersion,
            integrationEvent.OccurredAt,
            serializer.Serialize(integrationEvent));

        ResolveDbContext().Set<IntegrationEventOutboxMessageEntity>().Add(new IntegrationEventOutboxMessageEntity(
            Guid.CreateVersion7(),
            envelope,
            timeProvider.GetUtcNow()));

        return ValueTask.CompletedTask;
    }

    private TContext ResolveDbContext()
    {
        if (dbContext is not null)
        {
            return dbContext;
        }

        if (CurrentSaveChangesDbContext.Current is TContext currentDbContext)
        {
            return currentDbContext;
        }

        throw new InvalidOperationException($"No current {typeof(TContext).Name} SaveChanges context is available for the integration event outbox.");
    }
}
