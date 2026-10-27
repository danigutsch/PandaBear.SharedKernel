using Google.Protobuf;
using PandaBear.Messaging.IntegrationEvents;

namespace PandaBear.Messaging.Protobuf;

/// <summary>
/// Serializes integration events that are Protobuf messages as binary payloads, and delegates every other event to an optional fallback serializer.
/// </summary>
/// <remarks>
/// A Protobuf payload is carried as base64 text with <see cref="EventPayloadEncoding.Base64"/> and the
/// <see cref="ProtobufContentTypes.Payload"/> content type, which is how CloudEvents carries binary data
/// (<c>data_base64</c> with <c>datacontenttype: application/protobuf</c>). Register the serializer globally or keyed by context to
/// choose Protobuf for that context; pass the generated JSON serializer as the fallback to mix both formats.
/// </remarks>
/// <param name="fallback">The serializer for events that are not Protobuf messages, or <see langword="null"/> to reject them.</param>
public sealed class ProtobufIntegrationEventSerializer(IIntegrationEventSerializer? fallback = null) : IIntegrationEventSerializer
{

    /// <summary>
    /// Serializes an integration event to payload text: base64 for a Protobuf message, otherwise the fallback serializer's text.
    /// </summary>
    /// <param name="integrationEvent">The integration event to serialize.</param>
    /// <typeparam name="TIntegrationEvent">The integration event type.</typeparam>
    /// <returns>The serialized payload text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="integrationEvent"/> is null.</exception>
    /// <exception cref="NotSupportedException">Thrown when the event is not a Protobuf message and no fallback serializer is configured.</exception>
    public string Serialize<TIntegrationEvent>(TIntegrationEvent integrationEvent)
        where TIntegrationEvent : IIntegrationEvent =>
        SerializePayload(integrationEvent).Payload;

    /// <summary>
    /// Serializes an integration event with the encoding and content type that identify its format.
    /// </summary>
    /// <param name="integrationEvent">The integration event to serialize.</param>
    /// <typeparam name="TIntegrationEvent">The integration event type.</typeparam>
    /// <returns>A Base64 payload with <see cref="ProtobufContentTypes.Payload"/> for a Protobuf message, otherwise the fallback serializer's payload.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="integrationEvent"/> is null.</exception>
    /// <exception cref="NotSupportedException">Thrown when the event is not a Protobuf message and no fallback serializer is configured.</exception>
    public SerializedIntegrationEventPayload SerializePayload<TIntegrationEvent>(TIntegrationEvent integrationEvent)
        where TIntegrationEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent is IMessage message)
        {
            return new SerializedIntegrationEventPayload(
                Convert.ToBase64String(message.ToByteArray()),
                EventPayloadEncoding.Base64,
                ProtobufContentTypes.Payload);
        }

        if (fallback is not null)
        {
            return fallback.SerializePayload(integrationEvent);
        }

        throw new NotSupportedException(
            $"Integration event '{TIntegrationEvent.EventType}' ({typeof(TIntegrationEvent)}) is not a Protobuf message and no fallback serializer is configured. "
            + "Generate the event type from a .proto file so that it implements Google.Protobuf.IMessage, or pass a fallback serializer, such as the generated JSON serializer, to the constructor.");
    }

    /// <summary>
    /// Deserializes a Protobuf payload produced by <see cref="SerializePayload{TIntegrationEvent}"/>.
    /// </summary>
    /// <param name="payload">The base64 payload text.</param>
    /// <typeparam name="TIntegrationEvent">The integration event type, which must be a Protobuf message.</typeparam>
    /// <returns>The deserialized integration event.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="payload"/> is blank or is not valid base64.</exception>
    /// <exception cref="InvalidProtocolBufferException">Thrown when the bytes are not a valid message of <typeparamref name="TIntegrationEvent"/>.</exception>
    public static TIntegrationEvent Deserialize<TIntegrationEvent>(string payload)
        where TIntegrationEvent : IIntegrationEvent, IMessage<TIntegrationEvent>, new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(payload);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("The payload is not valid base64; a Protobuf payload must use EventPayloadEncoding.Base64.", nameof(payload), exception);
        }

        var integrationEvent = new TIntegrationEvent();
        integrationEvent.MergeFrom(bytes);
        return integrationEvent;
    }
}
