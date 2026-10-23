namespace PandaBear.EventSourcing.Tests;

public sealed class EventSourcingValueTests
{
    [Fact]
    public void StreamId_from_trims_value()
    {
        // Arrange
        const string value = " catalog-tour-tour-1 ";

        // Act
        var streamId = StreamId.From(value);

        // Assert
        streamId.Value.ShouldBe("catalog-tour-tour-1");
        streamId.ToString().ShouldBe("catalog-tour-tour-1");
    }

    [Fact]
    public void StreamId_from_rejects_null_value()
    {
        // Arrange
        string? value = null;

        // Act, Assert
        ShouldAssertionExtensions.ShouldThrow<ArgumentNullException>((Func<object?>)(() => StreamId.From(value)));
    }

    [Fact]
    public void StreamId_default_instance_rejects_value_access()
    {
        // Arrange
        var streamId = default(StreamId);

        // Act, Assert
        ShouldAssertionExtensions.ShouldThrow<InvalidOperationException>((Func<object?>)(() => streamId.Value));
        ShouldAssertionExtensions.ShouldThrow<InvalidOperationException>((Func<object?>)(() => streamId.ToString()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void StreamId_from_rejects_blank_values(string value)
    {
        // Act, Assert
        ShouldAssertionExtensions.ShouldThrow<ArgumentException>((Func<object?>)(() => StreamId.From(value)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void StreamRevision_from_rejects_non_positive_values(long value)
    {
        // Act, Assert
        ShouldAssertionExtensions.ShouldThrow<ArgumentOutOfRangeException>((Func<object?>)(() => StreamRevision.From(value)));
    }

    [Fact]
    public void StreamRevision_default_instance_rejects_value_access()
    {
        // Arrange
        var revision = default(StreamRevision);

        // Act, Assert
        ShouldAssertionExtensions.ShouldThrow<InvalidOperationException>((Func<object?>)(() => revision.Value));
    }

    [Fact]
    public void ExpectedStreamRevision_from_represents_specific_revision()
    {
        // Arrange
        var revision = StreamRevision.From(3);

        // Act
        var expectedRevision = ExpectedStreamRevision.From(revision);

        // Assert
        expectedRevision.Value.ShouldBe(3);
        expectedRevision.RequiresEmptyStream.ShouldBeFalse();
    }

    [Fact]
    public void ExpectedStreamRevision_any_accepts_any_current_revision()
    {
        // Act
        var expectedRevision = ExpectedStreamRevision.Any;

        // Assert
        expectedRevision.Value.ShouldBeNull();
        expectedRevision.RequiresEmptyStream.ShouldBeFalse();
    }

    [Fact]
    public void ExpectedStreamRevision_nostream_requires_empty_stream()
    {
        // Act
        var expectedRevision = ExpectedStreamRevision.NoStream;

        // Assert
        expectedRevision.Value.ShouldBeNull();
        expectedRevision.RequiresEmptyStream.ShouldBeTrue();
    }

    [Fact]
    public void EventEnvelope_stores_stream_event_metadata()
    {
        // Arrange
        var streamId = StreamId.From("catalog-tour-tour-1");
        var revision = StreamRevision.From(4);
        var eventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var data = new TourPublished("tour-1");
        var recordedAt = new DateTimeOffset(2026, 6, 21, 17, 30, 0, TimeSpan.Zero);

        // Act
        var envelope = new EventEnvelope(streamId, 12, revision, eventId, "TourPublished", data, recordedAt);

        // Assert
        envelope.StreamId.ShouldBe(streamId);
        envelope.Position.ShouldBe(12);
        envelope.Revision.ShouldBe(revision);
        envelope.EventId.ShouldBe(eventId);
        envelope.EventType.ShouldBe("TourPublished");
        envelope.Data.ShouldBe(data);
        envelope.RecordedAt.ShouldBe(recordedAt);
    }

    [Fact]
    public void ProjectionCheckpoint_stores_projection_position()
    {
        // Act
        var checkpoint = new ProjectionCheckpoint("catalog-tour-projection", 42);

        // Assert
        checkpoint.ProjectionName.ShouldBe("catalog-tour-projection");
        checkpoint.Position.ShouldBe(42);
    }

}
