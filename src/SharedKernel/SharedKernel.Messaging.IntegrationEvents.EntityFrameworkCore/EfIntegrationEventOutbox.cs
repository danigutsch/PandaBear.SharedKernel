using Microsoft.EntityFrameworkCore;

namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Stores integration events in the current EF Core unit of work.
/// </summary>
/// <typeparam name="TContext">The DbContext type that owns the outbox table.</typeparam>
internal sealed class EfIntegrationEventOutbox<TContext> : IIntegrationEventOutbox
    where TContext : DbContext
{
    private readonly TContext dbContext;
    private readonly TimeProvider timeProvider;
    private readonly IIntegrationEventSerializer serializer;

    /// <summary>
    /// Creates an outbox over the given context.
    /// </summary>
    /// <param name="dbContext">Context whose unit of work receives the messages.</param>
    /// <param name="timeProvider">Clock that stamps the enqueue time.</param>
    /// <param name="serializer">Serializer for the event payload.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dbContext"/>, <paramref name="timeProvider"/>, or <paramref name="serializer"/> is <see langword="null"/>.</exception>
    public EfIntegrationEventOutbox(
        TContext dbContext,
        TimeProvider timeProvider,
        IIntegrationEventSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(serializer);

        this.dbContext = dbContext;
        this.timeProvider = timeProvider;
        this.serializer = serializer;
    }

    /// <inheritdoc />
    public ValueTask Enqueue<TIntegrationEvent>(TIntegrationEvent integrationEvent, CancellationToken ct)
        where TIntegrationEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ct.ThrowIfCancellationRequested();

        dbContext.Set<IntegrationEventOutboxMessage>().Add(IntegrationEventOutboxMessageFactory.Create(
            integrationEvent,
            serializer,
            timeProvider.GetUtcNow()));

        return ValueTask.CompletedTask;
    }
}
