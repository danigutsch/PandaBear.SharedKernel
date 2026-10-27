using Microsoft.EntityFrameworkCore;

namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Publishes envelopes by adding transport messages for one consumer to the current DbContext, so they commit with the caller's unit of work.
/// </summary>
/// <param name="dbContext">Context that receives the transport messages.</param>
/// <param name="timeProvider">Clock that stamps the receive time.</param>
/// <param name="consumerName">Name of the consumer the messages are addressed to.</param>
internal sealed class PostgreSqlIntegrationEventTransportPublisher<TContext>(
    TContext dbContext,
    TimeProvider timeProvider,
    string consumerName) : IEventEnvelopePublisher
    where TContext : DbContext
{
    /// <inheritdoc />
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
