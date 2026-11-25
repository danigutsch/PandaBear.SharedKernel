namespace SharedKernel.Messaging;

/// <summary>
/// Exposes transport-neutral event envelope fields.
/// </summary>
public interface IEventEnvelope : IEventEnvelopeMetadata, IEventEnvelopePayload
{
    /// <summary>
    /// Gets the envelope specification name.
    /// </summary>
    string EnvelopeSpec { get; }

    /// <summary>
    /// Gets the envelope specification version.
    /// </summary>
    string EnvelopeSpecVersion { get; }

    /// <summary>
    /// Gets the optional serialized extension attribute object.
    /// </summary>
    string? ExtensionAttributesJson { get; }
}
