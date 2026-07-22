using System.Globalization;
using System.Text.Json;

namespace SharedKernel.Messaging.IntegrationEvents.Tests;

public sealed class GeneratedIntegrationEventDispatchTests
{
    [Fact]
    public void Contract_registration_rejects_an_event_type_that_disagrees_with_the_contract()
    {
        // Arrange
        var register = GeneratedIntegrationEventTestServices.CreateContractRegistration(
            "wrong.event.type");

        // Act
        var exception = register.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain(TestIntegrationEvent.EventType, StringComparison.Ordinal);
        exception.Message.ShouldContain("wrong.event.type", StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_uses_registered_json_metadata()
    {
        // Arrange
        var serializer = GeneratedIntegrationEventTestServices.CreateSerializer();
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
    public async Task Publish_resolves_one_scoped_handler_and_forwards_the_deserialized_event_and_cancellation()
    {
        // Arrange
        using var host = GeneratedIntegrationEventTestServices.CreateConsumerHost();
        using var delivery = host.OpenDelivery();
        var publisher = delivery.Publisher;
        var handler = delivery.Handler;
        var eventId = Guid.CreateVersion7();
        var occurredAt = DateTimeOffset.Parse("2026-07-05T12:30:00+00:00", CultureInfo.InvariantCulture);
        var integrationEvent = new TestIntegrationEvent(eventId, occurredAt, "Rio de Janeiro");
        var serializer = host.Serializer;
        var envelope = TestEventEnvelopeFactory.Create(integrationEvent, serializer.Serialize(integrationEvent));
        using var cancellationTokenSource = new CancellationTokenSource();
        using var secondDelivery = host.OpenDelivery();
        var secondHandler = secondDelivery.Handler;

        // Act
        await publisher.Publish(envelope, cancellationTokenSource.Token);

        // Assert
        var deliveredEvent = handler.IntegrationEvent.ShouldNotBeNull();
        deliveredEvent.EventId.ShouldBe(eventId);
        deliveredEvent.OccurredAt.ShouldBe(occurredAt);
        deliveredEvent.Name.ShouldBe("Rio de Janeiro");
        handler.CancellationToken.ShouldBe(cancellationTokenSource.Token);
        handler.InvocationCount.ShouldBe(1);
        ReferenceEquals(handler, secondHandler).ShouldBeFalse();
    }

    [Fact]
    public async Task Publish_delivers_two_registered_event_types_through_one_scoped_handler()
    {
        // Arrange
        using var host = GeneratedIntegrationEventTestServices.CreateConsumerHost();
        using var delivery = host.OpenDelivery();
        var occurredAt = DateTimeOffset.Parse("2026-07-05T12:30:00+00:00", CultureInfo.InvariantCulture);
        var created = new TestIntegrationEvent(Guid.CreateVersion7(), occurredAt, "Rio de Janeiro");
        var updated = new TestUpdatedIntegrationEvent(Guid.CreateVersion7(), occurredAt, "Rio Atualizado");
        var createdEnvelope = TestEventEnvelopeFactory.Create(created, host.Serializer.Serialize(created));
        var updatedEnvelope = TestEventEnvelopeFactory.Create(updated, host.Serializer.Serialize(updated));

        // Act
        await delivery.Publisher.Publish(createdEnvelope, CancellationToken.None);
        await delivery.Publisher.Publish(updatedEnvelope, CancellationToken.None);

        // Assert
        delivery.Handler.IntegrationEvent.ShouldNotBeNull().ShouldBe(created);
        delivery.Handler.UpdatedIntegrationEvent.ShouldNotBeNull().ShouldBe(updated);
        delivery.Handler.InvocationCount.ShouldBe(2);
    }

    [Fact]
    public async Task Publish_rejects_malformed_payload_before_invoking_the_handler()
    {
        // Arrange
        using var host = GeneratedIntegrationEventTestServices.CreateConsumerHost();
        using var delivery = host.OpenDelivery();
        var integrationEvent = new TestIntegrationEvent(
            Guid.CreateVersion7(),
            DateTimeOffset.Parse("2026-07-05T12:30:00+00:00", CultureInfo.InvariantCulture),
            "Rio de Janeiro");
        var envelope = TestEventEnvelopeFactory.Create(integrationEvent, "{");

        // Act
        Func<Task> publish = async () => await delivery.Publisher.Publish(envelope, CancellationToken.None);
        _ = await publish.ShouldThrow<JsonException>();

        // Assert
        delivery.Handler.InvocationCount.ShouldBe(0);
    }

    [Fact]
    public async Task Publish_rejects_precancelled_delivery_before_invoking_the_handler()
    {
        // Arrange
        using var host = GeneratedIntegrationEventTestServices.CreateConsumerHost();
        using var delivery = host.OpenDelivery();
        var integrationEvent = new TestIntegrationEvent(
            Guid.CreateVersion7(),
            DateTimeOffset.Parse("2026-07-05T12:30:00+00:00", CultureInfo.InvariantCulture),
            "Rio de Janeiro");
        var envelope = TestEventEnvelopeFactory.Create(integrationEvent, host.Serializer.Serialize(integrationEvent));
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act
        Func<Task> publish = async () => await delivery.Publisher.Publish(envelope, cancellationTokenSource.Token);
        _ = await publish.ShouldThrowAssignableTo<OperationCanceledException>();

        // Assert
        delivery.Handler.InvocationCount.ShouldBe(0);
    }

    [Fact]
    public void Disposing_the_delivery_scope_disposes_the_closed_typed_handler()
    {
        // Arrange
        using var host = GeneratedIntegrationEventTestServices.CreateConsumerHost();
        var delivery = host.OpenDelivery();
        var handler = delivery.Handler;

        // Act
        delivery.Dispose();

        // Assert
        handler.IsDisposed.ShouldBeTrue();
        handler.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public void Serialize_reports_runtime_type_for_unknown_events()
    {
        // Arrange
        var serializer = GeneratedIntegrationEventTestServices.CreateSerializer();
        var integrationEvent = new UnknownIntegrationEvent(Guid.CreateVersion7(), DateTimeOffset.UtcNow);

        // Act
        Action serialize = () => serializer.Serialize(integrationEvent);
        var exception = serialize.ShouldThrow<NotSupportedException>();

        // Assert
        var eventTypeName = typeof(UnknownIntegrationEvent).FullName.ShouldNotBeNull();
        exception.Message.ShouldContain(eventTypeName, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_rejects_an_unregistered_derived_event()
    {
        // Arrange
        var serializer = GeneratedIntegrationEventTestServices.CreateSerializer();
        var integrationEvent = new UnregisteredDerivedIntegrationEvent(
            Guid.CreateVersion7(),
            DateTimeOffset.UtcNow,
            "Rio de Janeiro",
            "must not be dropped");

        // Act
        Action serialize = () => serializer.Serialize(integrationEvent);
        var exception = serialize.ShouldThrow<NotSupportedException>();

        // Assert
        var eventTypeName = typeof(UnregisteredDerivedIntegrationEvent).FullName.ShouldNotBeNull();
        exception.Message.ShouldContain(eventTypeName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Publish_reports_unknown_envelope_event_type()
    {
        // Arrange
        using var host = GeneratedIntegrationEventTestServices.CreateConsumerHost();
        using var delivery = host.OpenDelivery();
        var publisher = delivery.Publisher;
        var integrationEvent = new TestIntegrationEvent(Guid.CreateVersion7(), DateTimeOffset.UtcNow, "unknown");
        var envelope = TestEventEnvelopeFactory.Create(integrationEvent, "{}", "unknown.event");

        // Act
        Func<Task> publish = async () => await publisher.Publish(envelope, CancellationToken.None);
        var exception = await publish.ShouldThrow<NotSupportedException>();

        // Assert
        exception.Message.ShouldContain("unknown.event", StringComparison.Ordinal);
    }
}
