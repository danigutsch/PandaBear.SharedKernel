namespace SharedKernel.Messaging.IntegrationEvents;

/// <summary>
/// Exposes durable outbox lifecycle metadata for an integration event message.
/// </summary>
public interface IIntegrationEventOutboxMessage
{
    /// <summary>
    /// Gets the outbox message identifier.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Gets when the message was enqueued.
    /// </summary>
    DateTimeOffset EnqueuedAt { get; }

    /// <summary>
    /// Gets when the message was published.
    /// </summary>
    DateTimeOffset? PublishedAt { get; }
}
