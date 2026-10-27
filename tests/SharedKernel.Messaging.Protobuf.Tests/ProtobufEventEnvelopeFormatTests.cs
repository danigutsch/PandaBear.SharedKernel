using System.Text.Json;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.Protobuf;

namespace PandaBear.Messaging.Protobuf.Tests;

/// <summary>Tests the CloudEvents Protobuf event format mapping of event envelopes.</summary>
public sealed class ProtobufEventEnvelopeFormatTests
{
    private static readonly DateTimeOffset Occurred = ProtobufTestEnvelopes.Occurred;

    [Fact]
    public void EncodeDecode_withjsonpayload_roundtripstheenvelope()
    {
        // Arrange
        var envelope = ProtobufTestEnvelopes.Create("""{"name":"Alps"}""", EventPayloadEncoding.Json, "application/json", """{"traceparent":"00-abc-def-01"}""");

        // Act
        var result = ProtobufEventEnvelopeFormat.Decode(ProtobufEventEnvelopeFormat.Encode(envelope));

        // Assert
        result.EnvelopeSpec.ShouldBe("cloudevents");
        result.EnvelopeSpecVersion.ShouldBe("1.0");
        result.EventId.ShouldBe(envelope.EventId);
        result.Source.ShouldBe(envelope.Source);
        result.EventType.ShouldBe("admin.tour.created");
        result.EventVersion.ShouldBe(2);
        result.Time.ShouldBe(Occurred);
        result.Subject.ShouldBe("tours/42");
        result.DataContentType.ShouldBe("application/json");
        result.DataSchema.ShouldBe(envelope.DataSchema);
        result.Payload.ShouldBe("""{"name":"Alps"}""");
        result.PayloadEncoding.ShouldBe(EventPayloadEncoding.Json);
        result.ExtensionAttributesJson.ShouldBe("""{"traceparent":"00-abc-def-01"}""");
    }

    [Fact]
    public void EncodeDecode_withbase64protobufpayload_roundtripsthebytes()
    {
        // Arrange
        byte[] bytes = [0x0A, 0x04, 0x41, 0x6C, 0x70, 0x73];
        var envelope = ProtobufTestEnvelopes.Create(Convert.ToBase64String(bytes), EventPayloadEncoding.Base64, ProtobufContentTypes.Payload);

        // Act
        var result = ProtobufEventEnvelopeFormat.Decode(ProtobufEventEnvelopeFormat.Encode(envelope));

        // Assert
        result.PayloadEncoding.ShouldBe(EventPayloadEncoding.Base64);
        result.DataContentType.ShouldBe("application/protobuf");
        Convert.FromBase64String(result.Payload!).ShouldBe(bytes);
        result.ExtensionAttributesJson.ShouldBeNull();
    }

    [Fact]
    public void Encode_storesjsonaspayloadtextandbase64asbinarydata()
    {
        // Arrange
        var formatter = new ProtobufEventFormatter();
        var json = ProtobufTestEnvelopes.Create("""{"name":"Alps"}""", EventPayloadEncoding.Json, "application/json");
        var binary = ProtobufTestEnvelopes.Create(Convert.ToBase64String([1, 2, 3]), EventPayloadEncoding.Base64, ProtobufContentTypes.Payload);

        // Act
        var jsonEvent = formatter.DecodeStructuredModeMessage(ProtobufEventEnvelopeFormat.Encode(json), contentType: null, extensionAttributes: null);
        var binaryEvent = formatter.DecodeStructuredModeMessage(ProtobufEventEnvelopeFormat.Encode(binary), contentType: null, extensionAttributes: null);

        // Assert
        jsonEvent.Data.ShouldBe("""{"name":"Alps"}""");
        binaryEvent.Data.ShouldBe(new byte[] { 1, 2, 3 });
        jsonEvent.SpecVersion.ShouldBe(CloudEventsSpecVersion.V1_0);
    }

    [Fact]
    public void EncodeDecode_withoutpayload_roundtripsanullpayload()
    {
        // Arrange
        var envelope = ProtobufTestEnvelopes.Create(null, EventPayloadEncoding.Json, null);

        // Act
        var result = ProtobufEventEnvelopeFormat.Decode(ProtobufEventEnvelopeFormat.Encode(envelope));

        // Assert
        result.Payload.ShouldBeNull();
        result.DataContentType.ShouldBeNull();
    }

    [Fact]
    public void Encode_withnonecloudeventsenvelope_throwsnotsupportedexception()
    {
        // Arrange
        var envelope = new EventEnvelope("other", "1.0", "id", new Uri("urn:s"), "t", null, null, null, null, null, null, EventPayloadEncoding.Json, null);
        Func<object?> act = () => ProtobufEventEnvelopeFormat.Encode(envelope);

        // Act
        var exception = act.ShouldThrow<NotSupportedException>();

        // Assert
        exception.Message.ShouldContain("'other' 1.0", StringComparison.Ordinal);
    }

    [Fact]
    public void Encode_withnonstringextensionattribute_throwsjsonexception()
    {
        // Arrange
        var envelope = ProtobufTestEnvelopes.Create(null, EventPayloadEncoding.Json, null, """{"attempt":3}""");
        Func<object?> act = () => ProtobufEventEnvelopeFormat.Encode(envelope);

        // Act, Assert
        act.ShouldThrow<JsonException>();
    }

    [Fact]
    public void Encode_withnullenvelope_throwsargumentnullexception()
    {
        // Arrange
        Func<object?> act = () => ProtobufEventEnvelopeFormat.Encode(null!);

        // Act, Assert
        act.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Encode_withnullextensionattribute_throwsjsonexception()
    {
        // Arrange
        var envelope = ProtobufTestEnvelopes.Create(null, EventPayloadEncoding.Json, null, """{"attempt":null}""");
        Func<object?> act = () => ProtobufEventEnvelopeFormat.Encode(envelope);

        // Act, Assert
        act.ShouldThrow<JsonException>();
    }

    [Fact]
    public void Decode_withprotodata_throwsnotsupportedexception()
    {
        // Arrange
        var cloudEvent = new CloudEvent
        {
            Id = "id",
            Source = new Uri("urn:test"),
            Type = "test.proto.data",
            Data = new Google.Protobuf.WellKnownTypes.StringValue { Value = "Alps" },
        };
        var message = new ProtobufEventFormatter().EncodeStructuredModeMessage(cloudEvent, out _);
        Func<object?> act = () => ProtobufEventEnvelopeFormat.Decode(message);

        // Act
        var exception = act.ShouldThrow<NotSupportedException>();

        // Assert
        exception.Message.ShouldContain("proto_data", StringComparison.Ordinal);
    }
}
