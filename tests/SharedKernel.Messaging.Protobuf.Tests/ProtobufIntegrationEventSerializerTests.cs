using Google.Protobuf;

namespace PandaBear.Messaging.Protobuf.Tests;

/// <summary>Tests Protobuf payload serialization, JSON fallback, and the errors for unsupported events.</summary>
public sealed class ProtobufIntegrationEventSerializerTests
{
    private static readonly DateTimeOffset Occurred = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_123);

    [Fact]
    public void SerializePayload_withprotobufmessage_returnsbase64withprotobufcontenttype()
    {
        // Arrange
        var integrationEvent = new ProtobufTestEvent { EventId = Guid.NewGuid(), OccurredAt = Occurred, Name = "Alps" };
        var serializer = new ProtobufIntegrationEventSerializer();

        // Act
        var payload = serializer.SerializePayload(integrationEvent);

        // Assert
        payload.Encoding.ShouldBe(EventPayloadEncoding.Base64);
        payload.DataContentType.ShouldBe("application/protobuf");
        Convert.FromBase64String(payload.Payload).ShouldBe(integrationEvent.ToByteArray());
        serializer.Serialize(integrationEvent).ShouldBe(payload.Payload);
    }

    [Fact]
    public void Deserialize_ofserializedpayload_roundtripstheevent()
    {
        // Arrange
        var integrationEvent = new ProtobufTestEvent { EventId = Guid.NewGuid(), OccurredAt = Occurred, Name = "Alps" };
        var payload = new ProtobufIntegrationEventSerializer().SerializePayload(integrationEvent);

        // Act
        var result = ProtobufIntegrationEventSerializer.Deserialize<ProtobufTestEvent>(payload.Payload);

        // Assert
        result.Equals(integrationEvent).ShouldBeTrue();
    }

    [Fact]
    public void SerializePayload_withjsononlyeventandfallback_delegatestothefallback()
    {
        // Arrange
        var serializer = new ProtobufIntegrationEventSerializer(new FixedJsonIntegrationEventSerializer());

        // Act
        var payload = serializer.SerializePayload(new JsonOnlyTestEvent(Guid.NewGuid(), Occurred));

        // Assert
        payload.Payload.ShouldBe("""{"fixed":true}""");
        payload.Encoding.ShouldBe(EventPayloadEncoding.Json);
        payload.DataContentType.ShouldBe("application/json");
    }

    [Fact]
    public void SerializePayload_withjsononlyeventandnofallback_throwsanactionableerror()
    {
        // Arrange
        var serializer = new ProtobufIntegrationEventSerializer();
        Func<object?> act = () => serializer.SerializePayload(new JsonOnlyTestEvent(Guid.NewGuid(), Occurred));

        // Act
        var exception = act.ShouldThrow<NotSupportedException>();

        // Assert
        exception.Message.ShouldContain("test.json.created", StringComparison.Ordinal);
        exception.Message.ShouldContain("fallback serializer", StringComparison.Ordinal);
    }

    [Fact]
    public void SerializePayload_withnullevent_throwsargumentnullexception()
    {
        // Arrange
        var serializer = new ProtobufIntegrationEventSerializer();
        Func<object?> act = () => serializer.SerializePayload<ProtobufTestEvent>(null!);

        // Act, Assert
        act.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Deserialize_withinvalidbase64_throwsargumentexception()
    {
        // Arrange
        Func<object?> act = () => ProtobufIntegrationEventSerializer.Deserialize<ProtobufTestEvent>("not base64!");

        // Act
        var exception = act.ShouldThrow<ArgumentException>();

        // Assert
        exception.Message.ShouldContain("EventPayloadEncoding.Base64", StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_withtruncatedmessage_throwsinvalidprotocolbufferexception()
    {
        // Arrange
        var truncated = Convert.ToBase64String([0x0A, 0x7F]);
        Func<object?> act = () => ProtobufIntegrationEventSerializer.Deserialize<ProtobufTestEvent>(truncated);

        // Act, Assert
        act.ShouldThrow<InvalidProtocolBufferException>();
    }
}
