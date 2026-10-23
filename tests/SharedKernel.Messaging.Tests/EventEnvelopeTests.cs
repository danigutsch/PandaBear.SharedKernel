namespace PandaBear.Messaging.Tests;

public sealed class EventEnvelopeTests
{
    private const string EnvelopeSpec = "test-envelope";
    private const string EnvelopeSpecVersion = "1.0";
    private const string JsonContentType = "application/json";

    [Fact]
    public void Creates_envelope_when_values_are_valid()
    {
        // Arrange
        var source = new Uri("urn:example:admin");
        var occurredAt = TimeProvider.System.GetUtcNow();

        // Act
        var envelope = new EventEnvelope(
            EnvelopeSpec,
            EnvelopeSpecVersion,
            "018fff35-7f1b-7d01-bf70-e33f71fcaeca",
            source,
            "catalog.media.original-stored.v1",
            1,
            occurredAt,
            "tour-1",
            JsonContentType,
            new Uri("https://schemas.example/events/tour-created.json"),
            "{}",
            EventPayloadEncoding.Json,
            "{\"traceparent\":\"00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00\"}");

        // Assert
        envelope.EnvelopeSpec.ShouldBe(EnvelopeSpec);
        envelope.EnvelopeSpecVersion.ShouldBe(EnvelopeSpecVersion);
        envelope.EventId.ShouldBe("018fff35-7f1b-7d01-bf70-e33f71fcaeca");
        envelope.Source.ShouldBe(source);
        envelope.EventType.ShouldBe("catalog.media.original-stored.v1");
        envelope.EventVersion.ShouldBe(1);
        envelope.Time.ShouldBe(occurredAt);
        envelope.Subject.ShouldBe("tour-1");
        envelope.DataContentType.ShouldBe(JsonContentType);
        envelope.DataSchema.ShouldBe(new Uri("https://schemas.example/events/tour-created.json"));
        envelope.Payload.ShouldBe("{}");
        envelope.PayloadEncoding.ShouldBe(EventPayloadEncoding.Json);
        envelope.ExtensionAttributesJson.ShouldContain("traceparent", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_blank_envelope_spec(string? envelopeSpec)
    {
        // Arrange
        Action create = () =>
        {
            _ = new EventEnvelope(
                envelopeSpec!,
                EnvelopeSpecVersion,
                "event-1",
                new Uri("urn:test"),
                "catalog.media.original-stored.v1",
                1,
                TimeProvider.System.GetUtcNow(),
                null,
                JsonContentType,
                null,
                "{}",
                EventPayloadEncoding.Json,
                null);
        };

        // Act
        var exception = create.ShouldThrow<ArgumentException>();

        // Assert
        exception.ParamName.ShouldBe("envelopeSpec");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_blank_envelope_spec_version(string? envelopeSpecVersion)
    {
        // Arrange
        Action create = () =>
        {
            _ = new EventEnvelope(
                EnvelopeSpec,
                envelopeSpecVersion!,
                "event-1",
                new Uri("urn:test"),
                "catalog.media.original-stored.v1",
                1,
                TimeProvider.System.GetUtcNow(),
                null,
                JsonContentType,
                null,
                "{}",
                EventPayloadEncoding.Json,
                null);
        };

        // Act
        var exception = create.ShouldThrow<ArgumentException>();

        // Assert
        exception.ParamName.ShouldBe("envelopeSpecVersion");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_blank_event_id(string? eventId)
    {
        // Arrange
        Action create = () =>
        {
            _ = new EventEnvelope(
                EnvelopeSpec,
                EnvelopeSpecVersion,
                eventId!,
                new Uri("urn:test"),
                "catalog.media.original-stored.v1",
                1,
                TimeProvider.System.GetUtcNow(),
                null,
                JsonContentType,
                null,
                "{}",
                EventPayloadEncoding.Json,
                null);
        };

        // Act
        var exception = create.ShouldThrow<ArgumentException>();

        // Assert
        exception.ParamName.ShouldBe("eventId");
    }

    [Fact]
    public void Rejects_null_source()
    {
        // Arrange
        Action create = () =>
        {
            _ = new EventEnvelope(
                EnvelopeSpec,
                EnvelopeSpecVersion,
                "event-1",
                null!,
                "catalog.media.original-stored.v1",
                1,
                TimeProvider.System.GetUtcNow(),
                null,
                JsonContentType,
                null,
                "{}",
                EventPayloadEncoding.Json,
                null);
        };

        // Act
        var exception = create.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("source");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_blank_event_type(string? eventType)
    {
        // Arrange
        Action create = () =>
        {
            _ = new EventEnvelope(
                EnvelopeSpec,
                EnvelopeSpecVersion,
                "event-1",
                new Uri("urn:test"),
                eventType!,
                1,
                TimeProvider.System.GetUtcNow(),
                null,
                JsonContentType,
                null,
                "{}",
                EventPayloadEncoding.Json,
                null);
        };

        // Act
        var exception = create.ShouldThrow<ArgumentException>();

        // Assert
        exception.ParamName.ShouldBe("eventType");
    }

    [Fact]
    public void Rejects_event_type_longer_than_limit()
    {
        // Arrange
        var eventType = new string('a', EventEnvelope.EventTypeMaxLength + 1);

        Action create = () =>
        {
            _ = new EventEnvelope(
                EnvelopeSpec,
                EnvelopeSpecVersion,
                "event-1",
                new Uri("urn:test"),
                eventType,
                1,
                TimeProvider.System.GetUtcNow(),
                null,
                JsonContentType,
                null,
                "{}",
                EventPayloadEncoding.Json,
                null);
        };

        // Act
        var exception = create.ShouldThrow<ArgumentException>();

        // Assert
        exception.ParamName.ShouldBe("eventType");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_non_positive_event_version(int eventVersion)
    {
        // Arrange
        Action create = () =>
        {
            _ = new EventEnvelope(
                EnvelopeSpec,
                EnvelopeSpecVersion,
                "event-1",
                new Uri("urn:test"),
                "catalog.media.original-stored.v1",
                eventVersion,
                TimeProvider.System.GetUtcNow(),
                null,
                JsonContentType,
                null,
                "{}",
                EventPayloadEncoding.Json,
                null);
        };

        // Act
        var exception = create.ShouldThrow<ArgumentOutOfRangeException>();

        // Assert
        exception.ParamName.ShouldBe("eventVersion");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_blank_payload_when_provided(string? payload)
    {
        // Arrange
        Action create = () =>
        {
            _ = new EventEnvelope(
                EnvelopeSpec,
                EnvelopeSpecVersion,
                "event-1",
                new Uri("urn:test"),
                "catalog.media.original-stored.v1",
                1,
                TimeProvider.System.GetUtcNow(),
                null,
                JsonContentType,
                null,
                payload,
                EventPayloadEncoding.Json,
                null);
        };

        // Act
        var exception = create.ShouldThrow<ArgumentException>();

        // Assert
        exception.ParamName.ShouldBe("payload");
    }
}
