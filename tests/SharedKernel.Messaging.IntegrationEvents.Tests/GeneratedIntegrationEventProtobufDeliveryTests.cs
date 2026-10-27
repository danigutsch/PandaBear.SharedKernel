using PandaBear.Messaging.Protobuf.TestContracts;

namespace PandaBear.Messaging.IntegrationEvents.Tests;

/// <summary>Tests that the generated publisher delivers Protobuf envelopes to their handler exactly once and still delivers JSON envelopes.</summary>
public sealed class GeneratedIntegrationEventProtobufDeliveryTests
{
    private static readonly DateTimeOffset Occurred = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_123);

    [Fact]
    public async Task Publish_withprotobufenvelope_deliversitoncethroughthegeneratedpublisher()
    {
        // Arrange
        CapturingTourCreatedHandler.Handled.Clear();
        using var host = GeneratedIntegrationEventTestServices.CreateConsumerHost();
        var integrationEvent = TourCreated.Create(Guid.NewGuid(), Occurred, "Alps");
        var payload = host.Serializer.SerializePayload(integrationEvent);
        var envelope = new EventEnvelope(
            "cloudevents", "1.0", integrationEvent.EventId.ToString("D"), new Uri("urn:test"), TourCreated.EventType, TourCreated.EventVersion,
            integrationEvent.OccurredAt, null, payload.DataContentType, null, payload.Payload, payload.Encoding, null);
        using var delivery = host.OpenDelivery();

        // Act
        await delivery.Publisher.Publish(envelope, TestContext.Current.CancellationToken);

        // Assert
        CapturingTourCreatedHandler.Handled.ShouldBe([integrationEvent]);
    }

    [Fact]
    public async Task Publish_withjsonenvelope_stilldeliverstothejsonhandler()
    {
        // Arrange
        using var host = GeneratedIntegrationEventTestServices.CreateConsumerHost();
        var integrationEvent = new TestIntegrationEvent(Guid.NewGuid(), Occurred, "Alps");
        var payload = host.Serializer.Serialize(integrationEvent);
        using var delivery = host.OpenDelivery();

        // Act
        await delivery.Publisher.Publish(TestEventEnvelopeFactory.Create(integrationEvent, payload), TestContext.Current.CancellationToken);

        // Assert
        host.Handlers.Where(static handler => handler.IntegrationEvent is not null).ShouldHaveSingleItem().IntegrationEvent.ShouldBe(integrationEvent);
    }
}
