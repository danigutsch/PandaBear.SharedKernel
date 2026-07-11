using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Messaging.IntegrationEvents.Tests;

public sealed class RegisteredIntegrationEventRegistryTests
{
    [Fact]
    public void Serialize_uses_registered_json_metadata()
    {
        // Arrange
        var serializer = RegisteredIntegrationEventTestServices.CreateSerializer();
        var eventId = Guid.CreateVersion7();
        var occurredAt = DateTimeOffset.Parse("2026-07-05T12:30:00+00:00", CultureInfo.InvariantCulture);
        var integrationEvent = new TestIntegrationEvent(eventId, occurredAt, "Rio de Janeiro");

        // Act
        var json = serializer.Serialize(integrationEvent);

        // Assert
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        root.GetProperty(nameof(TestIntegrationEvent.EventId)).GetGuid().ShouldBe(eventId);
        root.GetProperty(nameof(TestIntegrationEvent.OccurredAt)).GetDateTimeOffset().ShouldBe(occurredAt);
        root.GetProperty(nameof(TestIntegrationEvent.Name)).GetString().ShouldBe("Rio de Janeiro");
    }

    [Fact]
    public async Task Publish_delivers_registered_envelope_to_typed_handler()
    {
        // Arrange
        var handler = new CapturingIntegrationEventHandler();
        using var provider = RegisteredIntegrationEventTestServices.CreateConsumerProvider(handler);
        var publisher = provider.GetRequiredService<IEventEnvelopePublisher>();
        var eventId = Guid.CreateVersion7();
        var occurredAt = DateTimeOffset.Parse("2026-07-05T12:30:00+00:00", CultureInfo.InvariantCulture);
        var integrationEvent = new TestIntegrationEvent(eventId, occurredAt, "Rio de Janeiro");
        var serializer = provider.GetRequiredService<IIntegrationEventSerializer>();
        var envelope = new EventEnvelope(
            "cloudevents",
            "1.0",
            eventId.ToString(),
            new Uri("urn:test"),
            TestIntegrationEvent.EventType,
            TestIntegrationEvent.EventVersion,
            occurredAt,
            null,
            "application/json",
            null,
            serializer.Serialize(integrationEvent),
            EventPayloadEncoding.Json,
            null);
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await publisher.Publish(envelope, cancellationTokenSource.Token);

        // Assert
        var deliveredEvent = handler.IntegrationEvent.ShouldNotBeNull();
        deliveredEvent.EventId.ShouldBe(eventId);
        deliveredEvent.OccurredAt.ShouldBe(occurredAt);
        deliveredEvent.Name.ShouldBe("Rio de Janeiro");
        handler.CancellationToken.ShouldBe(cancellationTokenSource.Token);
    }

    [Fact]
    public void Serialize_reports_runtime_type_for_unknown_events()
    {
        // Arrange
        var serializer = RegisteredIntegrationEventTestServices.CreateSerializer();
        var integrationEvent = new UnknownIntegrationEvent(Guid.CreateVersion7(), DateTimeOffset.UtcNow);

        // Act
        Action serialize = () => serializer.Serialize(integrationEvent);
        var exception = serialize.ShouldThrow<NotSupportedException>();

        // Assert
        var eventTypeName = typeof(UnknownIntegrationEvent).FullName.ShouldNotBeNull();
        exception.Message.ShouldContain(eventTypeName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Publish_reports_unknown_envelope_event_type()
    {
        // Arrange
        using var provider = RegisteredIntegrationEventTestServices.CreateConsumerProvider();
        var publisher = provider.GetRequiredService<IEventEnvelopePublisher>();
        var envelope = new EventEnvelope(
            "cloudevents",
            "1.0",
            Guid.CreateVersion7().ToString(),
            new Uri("urn:test"),
            "unknown.event",
            1,
            DateTimeOffset.UtcNow,
            null,
            "application/json",
            null,
            "{}",
            EventPayloadEncoding.Json,
            null);

        // Act
        Func<Task> publish = async () => await publisher.Publish(envelope, CancellationToken.None);
        var exception = await publish.ShouldThrow<NotSupportedException>();

        // Assert
        exception.Message.ShouldContain("unknown.event", StringComparison.Ordinal);
    }
}
