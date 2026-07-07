using Microsoft.EntityFrameworkCore;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

internal sealed class PostgreSqlIntegrationEventTransportPublisher<TContext>(
    TContext dbContext,
    TimeProvider timeProvider,
    string consumerName) : IEventEnvelopePublisher
    where TContext : DbContext
{
    public async ValueTask Publish(EventEnvelope envelope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var message = new IntegrationEventTransportMessage(
            Guid.CreateVersion7(),
            consumerName,
            envelope,
            timeProvider.GetUtcNow());

        await dbContext.Set<IntegrationEventTransportMessage>().AddAsync(message, ct).ConfigureAwait(false);
    }
}
