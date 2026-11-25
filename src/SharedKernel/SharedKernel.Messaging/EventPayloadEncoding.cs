namespace SharedKernel.Messaging;

/// <summary>
/// Describes how an event payload is represented in an envelope.
/// </summary>
public enum EventPayloadEncoding
{
    /// <summary>
    /// The payload is a JSON value serialized as text.
    /// </summary>
    Json = 0,

    /// <summary>
    /// The payload is base64-encoded binary content.
    /// </summary>
    Base64 = 1,
}
