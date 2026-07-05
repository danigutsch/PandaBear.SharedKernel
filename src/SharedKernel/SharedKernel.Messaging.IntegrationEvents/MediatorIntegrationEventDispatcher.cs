using SharedKernel.Mediator;

namespace SharedKernel.Messaging.IntegrationEvents;

/// <summary>
/// Dispatches integration events through the SharedKernel mediator publisher.
/// </summary>
public sealed class MediatorIntegrationEventDispatcher : IIntegrationEventDispatcher
{
    private readonly IPublisher publisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediatorIntegrationEventDispatcher"/> class.
    /// </summary>
    /// <param name="publisher">The mediator publisher used to publish integration events.</param>
    public MediatorIntegrationEventDispatcher(IPublisher publisher)
    {
        ArgumentNullException.ThrowIfNull(publisher);

        this.publisher = publisher;
    }

    /// <inheritdoc />
    public ValueTask Dispatch<TIntegrationEvent>(TIntegrationEvent integrationEvent, CancellationToken ct)
        where TIntegrationEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return publisher.Publish(integrationEvent, ct);
    }
}
