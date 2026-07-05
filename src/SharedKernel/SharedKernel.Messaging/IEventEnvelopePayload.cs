namespace SharedKernel.Messaging;

/// <summary>
/// Exposes transport-neutral event payload metadata.
/// </summary>
public interface IEventEnvelopePayload
{
    /// <summary>
    /// Gets the optional payload content type.
    /// </summary>
    string? DataContentType { get; }

    /// <summary>
    /// Gets the optional payload schema.
    /// </summary>
    Uri? DataSchema { get; }

    /// <summary>
    /// Gets the optional serialized event payload.
    /// </summary>
    string? Payload { get; }

    /// <summary>
    /// Gets the payload encoding.
    /// </summary>
    EventPayloadEncoding PayloadEncoding { get; }
}
