namespace SharedKernel.Messaging.IntegrationEvents;

/// <summary>
/// Represents an event intended to cross bounded-context or process boundaries.
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>
    /// Gets the stable integration event type name.
    /// </summary>
    static abstract string EventType { get; }

    /// <summary>
    /// Gets the integration event contract version.
    /// </summary>
    static abstract int EventVersion { get; }

    /// <summary>
    /// Gets the event identifier used for idempotency and tracing.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// Gets the UTC instant when the event occurred.
    /// </summary>
    DateTimeOffset OccurredAt { get; }
}
