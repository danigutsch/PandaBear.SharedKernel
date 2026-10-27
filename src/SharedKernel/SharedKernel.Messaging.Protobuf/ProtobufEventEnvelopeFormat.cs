using System.Text.Json;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.Protobuf;

namespace PandaBear.Messaging.Protobuf;

/// <summary>
/// Encodes event envelopes in the CloudEvents Protobuf event format (<see cref="ProtobufContentTypes.EventFormat"/>) and decodes them back.
/// </summary>
/// <remarks>
/// A <see cref="EventPayloadEncoding.Json"/> payload is stored in <c>text_data</c>; a <see cref="EventPayloadEncoding.Base64"/> payload is
/// decoded and stored in <c>binary_data</c>. The envelope event version is the <c>eventversion</c> extension attribute, and every entry of
/// <see cref="IEventEnvelopePayload"/> extension attributes is a string extension attribute. A <c>proto_data</c> payload is not supported.
/// </remarks>
public static class ProtobufEventEnvelopeFormat
{
    private const string EnvelopeSpec = "cloudevents";
    private const string EventVersionAttributeName = "eventversion";

    private static readonly CloudEventAttribute EventVersionAttribute =
        CloudEventAttribute.CreateExtension(EventVersionAttributeName, CloudEventAttributeType.Integer);

    private static readonly ProtobufEventFormatter Formatter = new();

    /// <summary>
    /// Encodes an envelope as one CloudEvents Protobuf message.
    /// </summary>
    /// <param name="envelope">The envelope to encode.</param>
    /// <returns>The encoded event, to be sent with the <see cref="ProtobufContentTypes.EventFormat"/> media type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="envelope"/> is null.</exception>
    /// <exception cref="NotSupportedException">Thrown when the envelope specification is not CloudEvents 1.0.</exception>
    /// <exception cref="JsonException">Thrown when the extension attributes are not a JSON object of string values: malformed JSON, or a value that is not a string, including null.</exception>
    public static byte[] Encode(IEventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.EnvelopeSpec != EnvelopeSpec || envelope.EnvelopeSpecVersion != CloudEventsSpecVersion.V1_0.VersionId)
        {
            throw new NotSupportedException(
                $"The CloudEvents Protobuf format encodes only '{EnvelopeSpec}' {CloudEventsSpecVersion.V1_0.VersionId} envelopes; the envelope is '{envelope.EnvelopeSpec}' {envelope.EnvelopeSpecVersion}.");
        }

        var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
        {
            Id = envelope.EventId,
            Source = envelope.Source,
            Type = envelope.EventType,
            Time = envelope.Time,
            Subject = envelope.Subject,
            DataContentType = envelope.DataContentType,
            DataSchema = envelope.DataSchema,
        };

        if (envelope.EventVersion is { } eventVersion)
        {
            cloudEvent[EventVersionAttribute] = eventVersion;
        }

        if (envelope.ExtensionAttributesJson is { } extensionAttributesJson)
        {
            using var document = JsonDocument.Parse(extensionAttributesJson);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    throw new JsonException($"Extension attribute '{property.Name}' must be a JSON string.");
                }

                cloudEvent[CloudEventAttribute.CreateExtension(property.Name, CloudEventAttributeType.String)] = property.Value.GetString()!;
            }
        }

        if (envelope.Payload is { } payload)
        {
            cloudEvent.Data = envelope.PayloadEncoding == EventPayloadEncoding.Base64 ? Convert.FromBase64String(payload) : payload;
        }

        return Formatter.EncodeStructuredModeMessage(cloudEvent, out _).ToArray();
    }

    /// <summary>
    /// Decodes a CloudEvents Protobuf message into an envelope.
    /// </summary>
    /// <param name="message">The encoded event.</param>
    /// <returns>The decoded envelope.</returns>
    /// <exception cref="NotSupportedException">Thrown when the event carries <c>proto_data</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when the message is not a valid CloudEvent.</exception>
    public static EventEnvelope Decode(ReadOnlyMemory<byte> message)
    {
        var cloudEvent = Formatter.DecodeStructuredModeMessage(message, contentType: null, extensionAttributes: [EventVersionAttribute]);

        int? eventVersion = null;
        var extensions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (attribute, value) in cloudEvent.GetPopulatedAttributes())
        {
            if (attribute.Name == EventVersionAttributeName)
            {
                eventVersion = (int)value;
            }
            else if (attribute.IsExtension)
            {
                extensions[attribute.Name] = attribute.Type.Format(value);
            }
        }

        string? payload = null;
        var encoding = EventPayloadEncoding.Json;
        switch (cloudEvent.Data)
        {
            case null:
                break;
            case string text:
                payload = text;
                break;
            case byte[] bytes:
                payload = Convert.ToBase64String(bytes);
                encoding = EventPayloadEncoding.Base64;
                break;
            default:
                throw new NotSupportedException(
                    $"The event carries '{cloudEvent.Data.GetType()}' data; only text_data and binary_data are supported, not proto_data.");
        }

        return new EventEnvelope(
            EnvelopeSpec,
            cloudEvent.SpecVersion.VersionId,
            cloudEvent.Id!,
            cloudEvent.Source!,
            cloudEvent.Type!,
            eventVersion,
            cloudEvent.Time,
            cloudEvent.Subject,
            cloudEvent.DataContentType,
            cloudEvent.DataSchema,
            payload,
            encoding,
            extensions.Count == 0 ? null : JsonSerializer.Serialize(extensions, ProtobufEventEnvelopeJsonContext.Default.DictionaryStringString));
    }
}
