using PandaBear.Messaging;
using PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Tests that outbox rows record the payload, encoding, and content type the serializer reports.
/// </summary>
public sealed class IntegrationEventOutboxMessageFactoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_withdefaultserializer_recordsjsonpayload()
    {
        // Arrange
        var integrationEvent = new ConsumerTestIntegrationEvent(Guid.CreateVersion7(), Now, Guid.CreateVersion7(), "code", "name");

        // Act
        var message = IntegrationEventOutboxMessageFactory.Create(integrationEvent, new ComposedMessagingTestIntegrationEventSerializer(), Now);

        // Assert
        message.Payload.ShouldBe("{}");
        message.PayloadEncoding.ShouldBe(EventPayloadEncoding.Json);
        message.DataContentType.ShouldBe("application/json");
    }

    [Fact]
    public void Create_withbinaryserializer_recordsthereportedencodingandcontenttype()
    {
        // Arrange
        var integrationEvent = new ConsumerTestIntegrationEvent(Guid.CreateVersion7(), Now, Guid.CreateVersion7(), "code", "name");

        // Act
        var message = IntegrationEventOutboxMessageFactory.Create(integrationEvent, new Base64TestIntegrationEventSerializer(), Now);

        // Assert
        message.Payload.ShouldBe("CgRBbHBz");
        message.PayloadEncoding.ShouldBe(EventPayloadEncoding.Base64);
        message.DataContentType.ShouldBe("application/protobuf");
    }
}
