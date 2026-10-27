namespace PandaBear.Messaging.IntegrationEvents;

/// <summary>
/// Carries a serialized integration event payload together with the encoding and content type that identify its format.
/// </summary>
/// <param name="Payload">The payload text: JSON text for <see cref="EventPayloadEncoding.Json"/>, or base64 text for <see cref="EventPayloadEncoding.Base64"/>.</param>
/// <param name="Encoding">How <paramref name="Payload"/> represents the serialized event.</param>
/// <param name="DataContentType">The media type of the serialized event, such as <c>application/json</c> or <c>application/protobuf</c>.</param>
public sealed record SerializedIntegrationEventPayload(string Payload, EventPayloadEncoding Encoding, string DataContentType);
