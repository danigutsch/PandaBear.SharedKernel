namespace SharedKernel.Messaging.IntegrationEvents;

/// <summary>
/// Exposes durable inbox lifecycle metadata for an integration event message.
/// </summary>
public interface IIntegrationEventInboxMessage
{
    /// <summary>
    /// Gets the inbox message identifier.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Gets when the message was received.
    /// </summary>
    DateTimeOffset ReceivedAt { get; }

    /// <summary>
    /// Gets when the message was processed.
    /// </summary>
    DateTimeOffset? ProcessedAt { get; }
}
