
namespace PandaBear.Messaging.IntegrationEvents.CloudEvents.Tests;

public sealed class CloudEventMapperTests
{
    [Fact]
    public void ToCloudEvent_maps_typed_event_metadata_to_a_cloudevents_envelope()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var occurredAt = TimeProvider.System.GetUtcNow();
        var integrationEvent = new TestIntegrationEvent(eventId, occurredAt, "tour-created");
        var source = new Uri("https://events.example/admin/tours");
        var dataSchema = new Uri("https://events.example/schemas/admin.tour.created.v1.json");
        var metadata = new CloudEventMetadata(source, "tour-42", "application/json", dataSchema);

        // Act
        var envelope = CloudEventMapper.ToCloudEvent(integrationEvent, metadata);

        // Assert
        envelope.Id.ShouldBe(eventId.ToString("D"));
        envelope.Source.ShouldBe(source);
        envelope.Type.ShouldBe("admin.tour.created");
        envelope.SpecVersion.ShouldBe("1.0");
        envelope.Time.ShouldBe(occurredAt);
        envelope.Subject.ShouldBe("tour-42");
        envelope.DataContentType.ShouldBe("application/json");
        envelope.DataSchema.ShouldBe(dataSchema);
        envelope.Version.ShouldBe(1);
        envelope.Data.ShouldBeSameAs(integrationEvent);
    }

    [Fact]
    public void ToCloudEvent_preserves_optional_null_metadata()
    {
        // Arrange
        var integrationEvent = new TestIntegrationEvent(Guid.NewGuid(), TimeProvider.System.GetUtcNow(), "tour-created");
        var metadata = new CloudEventMetadata(new Uri("https://events.example/admin/tours"));

        // Act
        var envelope = CloudEventMapper.ToCloudEvent(integrationEvent, metadata);

        // Assert
        envelope.Subject.ShouldBeNull();
        envelope.DataContentType.ShouldBeNull();
        envelope.DataSchema.ShouldBeNull();
    }

    [Fact]
    public void ToCloudEvent_rejects_null_integration_events()
    {
        // Arrange
        var metadata = new CloudEventMetadata(new Uri("https://events.example/admin/tours"));
        var method = typeof(CloudEventMapper).GetMethod(nameof(CloudEventMapper.ToCloudEvent)).ShouldNotBeNull();
        var genericMethod = method.MakeGenericMethod(typeof(TestIntegrationEvent));

        // Act
        Action invoke = () => genericMethod.Invoke(null, [null, metadata]);
        var argumentException = invoke.ShouldThrowInner<ArgumentNullException>();

        // Assert
        argumentException.ParamName.ShouldBe("integrationEvent");
    }

    [Fact]
    public void ToCloudEvent_rejects_null_metadata()
    {
        // Arrange
        var integrationEvent = new TestIntegrationEvent(Guid.NewGuid(), TimeProvider.System.GetUtcNow(), "tour-created");
        var method = typeof(CloudEventMapper).GetMethod(nameof(CloudEventMapper.ToCloudEvent)).ShouldNotBeNull();
        var genericMethod = method.MakeGenericMethod(typeof(TestIntegrationEvent));

        // Act
        Action invoke = () => genericMethod.Invoke(null, [integrationEvent, null]);
        var argumentException = invoke.ShouldThrowInner<ArgumentNullException>();

        // Assert
        argumentException.ParamName.ShouldBe("metadata");
    }
}
