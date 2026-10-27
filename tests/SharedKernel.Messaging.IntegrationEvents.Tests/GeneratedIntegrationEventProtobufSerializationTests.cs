using Google.Protobuf;
using PandaBear.Messaging.Protobuf.TestContracts;

namespace PandaBear.Messaging.IntegrationEvents.Tests;

/// <summary>Tests that the generated serializer serializes a mapped Protobuf event as binary and a JSON event as JSON.</summary>
public sealed class GeneratedIntegrationEventProtobufSerializationTests
{
    private static readonly DateTimeOffset Occurred = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_123);

    [Fact]
    public void SerializePayload_withmappedprotobufevent_returnsbase64withprotobufcontenttype()
    {
        // Arrange
        var serializer = GeneratedIntegrationEventTestServices.CreateSerializer();
        var integrationEvent = TourCreated.Create(Guid.NewGuid(), Occurred, "Alps");

        // Act
        var payload = serializer.SerializePayload(integrationEvent);

        // Assert
        payload.Encoding.ShouldBe(EventPayloadEncoding.Base64);
        payload.DataContentType.ShouldBe("application/protobuf");
        Convert.FromBase64String(payload.Payload).ShouldBe(integrationEvent.ToByteArray());
        serializer.Serialize(integrationEvent).ShouldBe(payload.Payload);
    }

    [Fact]
    public void SerializePayload_withjsonevent_keepsjsonencodingandcontenttype()
    {
        // Arrange
        var serializer = GeneratedIntegrationEventTestServices.CreateSerializer();
        var integrationEvent = new TestIntegrationEvent(Guid.NewGuid(), Occurred, "Alps");

        // Act
        var payload = serializer.SerializePayload(integrationEvent);

        // Assert
        payload.Encoding.ShouldBe(EventPayloadEncoding.Json);
        payload.DataContentType.ShouldBe("application/json");
        payload.Payload.ShouldContain("Alps", StringComparison.Ordinal);
    }
}
