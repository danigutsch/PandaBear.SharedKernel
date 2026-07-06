namespace SharedKernel.Messaging;

/// <summary>
/// Exposes transport-neutral event identity and routing metadata.
/// </summary>
public interface IEventEnvelopeMetadata
{
    /// <summary>
    /// Gets the event identifier used for idempotency and tracing.
    /// </summary>
    string EventId { get; }

    /// <summary>
    /// Gets the event source.
    /// </summary>
    Uri Source { get; }

    /// <summary>
    /// Gets the stable event type identifier.
    /// </summary>
    string EventType { get; }

    /// <summary>
    /// Gets the optional event contract version.
    /// </summary>
    int? EventVersion { get; }

    /// <summary>
    /// Gets the time at which the event occurred.
    /// </summary>
    DateTimeOffset? Time { get; }

    /// <summary>
    /// Gets the optional event subject.
    /// </summary>
    string? Subject { get; }
}
