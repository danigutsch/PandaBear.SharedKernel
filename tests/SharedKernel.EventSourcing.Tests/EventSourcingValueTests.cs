namespace SharedKernel.EventSourcing.Tests;

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
        TestAssert.Equal("catalog-tour-tour-1", streamId.Value);
        TestAssert.Equal("catalog-tour-tour-1", streamId.ToString());
    }

    [Fact]
    public void StreamId_from_rejects_null_value()
    {
        // Arrange
        string? value = null;

        // Act, Assert
        TestAssert.Throws<ArgumentNullException>(() => StreamId.From(value));
    }

    [Fact]
    public void StreamId_default_instance_rejects_value_access()
    {
        // Arrange
        var streamId = default(StreamId);

        // Act, Assert
        TestAssert.Throws<InvalidOperationException>(() => streamId.Value);
        TestAssert.Throws<InvalidOperationException>(() => streamId.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void StreamId_from_rejects_blank_values(string value)
    {
        // Arrange, Act, Assert
        TestAssert.Throws<ArgumentException>(() => StreamId.From(value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void StreamRevision_from_rejects_non_positive_values(long value)
    {
        // Arrange, Act, Assert
        TestAssert.Throws<ArgumentOutOfRangeException>(() => StreamRevision.From(value));
    }

    [Fact]
    public void StreamRevision_default_instance_rejects_value_access()
    {
        // Arrange
        var revision = default(StreamRevision);

        // Act, Assert
        TestAssert.Throws<InvalidOperationException>(() => revision.Value);
    }

    [Fact]
    public void ExpectedStreamRevision_from_represents_specific_revision()
    {
        // Arrange
        var revision = StreamRevision.From(3);

        // Act
        var expectedRevision = ExpectedStreamRevision.From(revision);

        // Assert
        TestAssert.Equal(3, expectedRevision.Value);
        TestAssert.False(expectedRevision.RequiresEmptyStream);
    }

    [Fact]
    public void ExpectedStreamRevision_any_accepts_any_current_revision()
    {
        // Arrange, Act
        var expectedRevision = ExpectedStreamRevision.Any;

        // Assert
        TestAssert.Null(expectedRevision.Value);
        TestAssert.False(expectedRevision.RequiresEmptyStream);
    }

    [Fact]
    public void ExpectedStreamRevision_nostream_requires_empty_stream()
    {
        // Arrange, Act
        var expectedRevision = ExpectedStreamRevision.NoStream;

        // Assert
        TestAssert.Null(expectedRevision.Value);
        TestAssert.True(expectedRevision.RequiresEmptyStream);
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
        TestAssert.Equal(streamId, envelope.StreamId);
        TestAssert.Equal(12, envelope.Position);
        TestAssert.Equal(revision, envelope.Revision);
        TestAssert.Equal(eventId, envelope.EventId);
        TestAssert.Equal("TourPublished", envelope.EventType);
        TestAssert.Equal(data, envelope.Data);
        TestAssert.Equal(recordedAt, envelope.RecordedAt);
    }

    [Fact]
    public void ProjectionCheckpoint_stores_projection_position()
    {
        // Arrange, Act
        var checkpoint = new ProjectionCheckpoint("catalog-tour-projection", 42);

        // Assert
        TestAssert.Equal("catalog-tour-projection", checkpoint.ProjectionName);
        TestAssert.Equal(42, checkpoint.Position);
    }

}
