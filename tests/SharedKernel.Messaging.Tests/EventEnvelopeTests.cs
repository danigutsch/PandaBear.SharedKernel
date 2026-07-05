namespace SharedKernel.Messaging.Tests;

public sealed class EventEnvelopeTests
{
    [Fact]
    public void Creates_envelope_when_values_are_valid()
    {
        var eventId = Guid.CreateVersion7();
        var occurredAt = DateTimeOffset.UtcNow;

        var envelope = new EventEnvelope(
            eventId,
            "catalog.media.original-stored.v1",
            1,
            occurredAt,
            "{}");

        envelope.EventId.ShouldBe(eventId);
        envelope.EventType.ShouldBe("catalog.media.original-stored.v1");
        envelope.EventVersion.ShouldBe(1);
        envelope.OccurredAt.ShouldBe(occurredAt);
        envelope.PayloadJson.ShouldBe("{}");
    }

    [Fact]
    public void Rejects_empty_event_id()
    {
        Action create = () =>
        {
            _ = new EventEnvelope(
                Guid.Empty,
                "catalog.media.original-stored.v1",
                1,
                DateTimeOffset.UtcNow,
                "{}");
        };

        var exception = create.ShouldThrow<ArgumentException>();

        exception.ParamName.ShouldBe("eventId");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_blank_event_type(string? eventType)
    {
        Action create = () =>
        {
            _ = new EventEnvelope(
                Guid.CreateVersion7(),
                eventType!,
                1,
                DateTimeOffset.UtcNow,
                "{}");
        };

        var exception = create.ShouldThrow<ArgumentException>();

        exception.ParamName.ShouldBe("eventType");
    }

    [Fact]
    public void Rejects_null_event_type()
    {
        Action create = () =>
        {
            _ = new EventEnvelope(
                Guid.CreateVersion7(),
                null!,
                1,
                DateTimeOffset.UtcNow,
                "{}");
        };

        var exception = create.ShouldThrow<ArgumentNullException>();

        exception.ParamName.ShouldBe("eventType");
    }

    [Fact]
    public void Rejects_event_type_longer_than_limit()
    {
        var eventType = new string('a', EventEnvelope.EventTypeMaxLength + 1);

        Action create = () =>
        {
            _ = new EventEnvelope(
                Guid.CreateVersion7(),
                eventType,
                1,
                DateTimeOffset.UtcNow,
                "{}");
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
                Guid.CreateVersion7(),
                "catalog.media.original-stored.v1",
                eventVersion,
                DateTimeOffset.UtcNow,
                "{}");
        };

        var exception = create.ShouldThrow<ArgumentOutOfRangeException>();

        exception.ParamName.ShouldBe("eventVersion");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_blank_payload_json(string? payloadJson)
    {
        Action create = () =>
        {
            _ = new EventEnvelope(
                Guid.CreateVersion7(),
                "catalog.media.original-stored.v1",
                1,
                DateTimeOffset.UtcNow,
                payloadJson!);
        };

        var exception = create.ShouldThrow<ArgumentException>();

        exception.ParamName.ShouldBe("payloadJson");
    }

    [Fact]
    public void Rejects_null_payload_json()
    {
        Action create = () =>
        {
            _ = new EventEnvelope(
                Guid.CreateVersion7(),
                "catalog.media.original-stored.v1",
                1,
                DateTimeOffset.UtcNow,
                null!);
        };

        var exception = create.ShouldThrow<ArgumentNullException>();

        exception.ParamName.ShouldBe("payloadJson");
    }
}
