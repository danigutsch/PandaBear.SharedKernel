namespace SharedKernel.Messaging.Tests;

public sealed class EventEnvelopeTests
{
    [Fact]
    public void Creates_envelope_when_values_are_valid()
    {
        var source = new Uri("urn:viajantes:admin");
        var occurredAt = DateTimeOffset.UtcNow;

        var envelope = new EventEnvelope(
            EventEnvelope.CloudEventsSpec,
            EventEnvelope.CloudEventsSpecVersion,
            "018fff35-7f1b-7d01-bf70-e33f71fcaeca",
            source,
            "catalog.media.original-stored.v1",
            1,
            occurredAt,
            "tour-1",
            "application/json",
            new Uri("https://schemas.example/events/tour-created.json"),
            "{}",
            EventPayloadEncoding.Json,
            "{\"traceparent\":\"00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00\"}");

        envelope.EnvelopeSpec.ShouldBe(EventEnvelope.CloudEventsSpec);
        envelope.EnvelopeSpecVersion.ShouldBe(EventEnvelope.CloudEventsSpecVersion);
        envelope.EventId.ShouldBe("018fff35-7f1b-7d01-bf70-e33f71fcaeca");
        envelope.Source.ShouldBe(source);
        envelope.EventType.ShouldBe("catalog.media.original-stored.v1");
        envelope.EventVersion.ShouldBe(1);
        envelope.Time.ShouldBe(occurredAt);
        envelope.Subject.ShouldBe("tour-1");
        envelope.DataContentType.ShouldBe("application/json");
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
        Action create = () =>
        {
            _ = new EventEnvelope(
                envelopeSpec!,
                EventEnvelope.CloudEventsSpecVersion,
                "event-1",
                new Uri("urn:test"),
                "catalog.media.original-stored.v1",
                1,
                DateTimeOffset.UtcNow,
                null,
                "application/json",
                null,
                "{}",
                EventPayloadEncoding.Json,
                null);
        };

        var exception = create.ShouldThrow<ArgumentException>();

        exception.ParamName.ShouldBe("envelopeSpec");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_blank_envelope_spec_version(string? envelopeSpecVersion)
    {
        Action create = () =>
        {
            _ = new EventEnvelope(
                EventEnvelope.CloudEventsSpec,
                envelopeSpecVersion!,
                "event-1",
                new Uri("urn:test"),
                "catalog.media.original-stored.v1",
                1,
                DateTimeOffset.UtcNow,
                null,
                "application/json",
                null,
                "{}",
                EventPayloadEncoding.Json,
                null);
        };

        var exception = create.ShouldThrow<ArgumentException>();

        exception.ParamName.ShouldBe("envelopeSpecVersion");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_blank_event_id(string? eventId)
    {
        Action create = () =>
        {
            _ = new EventEnvelope(
                EventEnvelope.CloudEventsSpec,
                EventEnvelope.CloudEventsSpecVersion,
                eventId!,
                new Uri("urn:test"),
                "catalog.media.original-stored.v1",
                1,
                DateTimeOffset.UtcNow,
                null,
                "application/json",
                null,
                "{}",
                EventPayloadEncoding.Json,
                null);
        };

        var exception = create.ShouldThrow<ArgumentException>();

        exception.ParamName.ShouldBe("eventId");
    }

    [Fact]
    public void Rejects_null_source()
    {
        Action create = () =>
        {
            _ = new EventEnvelope(
                EventEnvelope.CloudEventsSpec,
                EventEnvelope.CloudEventsSpecVersion,
                "event-1",
                null!,
                "catalog.media.original-stored.v1",
                1,
                DateTimeOffset.UtcNow,
                null,
                "application/json",
                null,
                "{}",
                EventPayloadEncoding.Json,
                null);
        };

        var exception = create.ShouldThrow<ArgumentNullException>();

        exception.ParamName.ShouldBe("source");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_blank_event_type(string? eventType)
    {
        Action create = () =>
        {
            _ = new EventEnvelope(
                EventEnvelope.CloudEventsSpec,
                EventEnvelope.CloudEventsSpecVersion,
                "event-1",
                new Uri("urn:test"),
                eventType!,
                1,
                DateTimeOffset.UtcNow,
                null,
                "application/json",
                null,
                "{}",
                EventPayloadEncoding.Json,
                null);
        };

        var exception = create.ShouldThrow<ArgumentException>();

        exception.ParamName.ShouldBe("eventType");
    }

    [Fact]
    public void Rejects_event_type_longer_than_limit()
    {
        var eventType = new string('a', EventEnvelope.EventTypeMaxLength + 1);

        Action create = () =>
        {
            _ = new EventEnvelope(
                EventEnvelope.CloudEventsSpec,
                EventEnvelope.CloudEventsSpecVersion,
                "event-1",
                new Uri("urn:test"),
                eventType,
                1,
                DateTimeOffset.UtcNow,
                null,
                "application/json",
                null,
                "{}",
                EventPayloadEncoding.Json,
                null);
        };

        var exception = create.ShouldThrow<ArgumentException>();

        exception.ParamName.ShouldBe("eventType");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_non_positive_event_version(int eventVersion)
    {
        Action create = () =>
        {
            _ = new EventEnvelope(
                EventEnvelope.CloudEventsSpec,
                EventEnvelope.CloudEventsSpecVersion,
                "event-1",
                new Uri("urn:test"),
                "catalog.media.original-stored.v1",
                eventVersion,
                DateTimeOffset.UtcNow,
                null,
                "application/json",
                null,
                "{}",
                EventPayloadEncoding.Json,
                null);
        };

        var exception = create.ShouldThrow<ArgumentOutOfRangeException>();

        exception.ParamName.ShouldBe("eventVersion");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_blank_payload_when_provided(string? payload)
    {
        Action create = () =>
        {
            _ = new EventEnvelope(
                EventEnvelope.CloudEventsSpec,
                EventEnvelope.CloudEventsSpecVersion,
                "event-1",
                new Uri("urn:test"),
                "catalog.media.original-stored.v1",
                1,
                DateTimeOffset.UtcNow,
                null,
                "application/json",
                null,
                payload,
                EventPayloadEncoding.Json,
                null);
        };

        var exception = create.ShouldThrow<ArgumentException>();

        exception.ParamName.ShouldBe("payload");
    }
}
