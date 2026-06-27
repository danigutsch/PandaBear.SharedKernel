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
        Assert.Equal("catalog-tour-tour-1", streamId.Value);
        Assert.Equal("catalog-tour-tour-1", streamId.ToString());
    }

    [Fact]
    public void StreamId_from_rejects_null_value()
    {
        // Arrange
        string? value = null;

        // Act, Assert
        Assert.Throws<ArgumentNullException>(() => StreamId.From(value));
    }

    [Fact]
    public void StreamId_default_instance_rejects_value_access()
    {
        // Arrange
        var streamId = default(StreamId);

        // Act, Assert
        Assert.Throws<InvalidOperationException>(() => streamId.Value);
        Assert.Throws<InvalidOperationException>(() => streamId.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void StreamId_from_rejects_blank_values(string value)
    {
        // Arrange, Act, Assert
        Assert.Throws<ArgumentException>(() => StreamId.From(value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void StreamRevision_from_rejects_non_positive_values(long value)
    {
        // Arrange, Act, Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => StreamRevision.From(value));
    }

    [Fact]
    public void StreamRevision_default_instance_rejects_value_access()
    {
        // Arrange
        var revision = default(StreamRevision);

        // Act, Assert
        Assert.Throws<InvalidOperationException>(() => revision.Value);
    }

    [Fact]
    public void ExpectedStreamRevision_from_represents_specific_revision()
    {
        // Arrange
        var revision = StreamRevision.From(3);

        // Act
        var expectedRevision = ExpectedStreamRevision.From(revision);

        // Assert
        Assert.Equal(3, expectedRevision.Value);
        Assert.False(expectedRevision.RequiresEmptyStream);
    }

    [Fact]
    public void ExpectedStreamRevision_any_accepts_any_current_revision()
    {
        // Arrange, Act
        var expectedRevision = ExpectedStreamRevision.Any;

        // Assert
        Assert.Null(expectedRevision.Value);
        Assert.False(expectedRevision.RequiresEmptyStream);
    }

    [Fact]
    public void ExpectedStreamRevision_noStream_requires_empty_stream()
    {
        // Arrange, Act
        var expectedRevision = ExpectedStreamRevision.NoStream;

        // Assert
        Assert.Null(expectedRevision.Value);
        Assert.True(expectedRevision.RequiresEmptyStream);
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
        Assert.Equal(streamId, envelope.StreamId);
        Assert.Equal(12, envelope.Position);
        Assert.Equal(revision, envelope.Revision);
        Assert.Equal(eventId, envelope.EventId);
        Assert.Equal("TourPublished", envelope.EventType);
        Assert.Equal(data, envelope.Data);
        Assert.Equal(recordedAt, envelope.RecordedAt);
    }

    [Fact]
    public void ProjectionCheckpoint_stores_projection_position()
    {
        // Arrange, Act
        var checkpoint = new ProjectionCheckpoint("catalog-tour-projection", 42);

        // Assert
        Assert.Equal("catalog-tour-projection", checkpoint.ProjectionName);
        Assert.Equal(42, checkpoint.Position);
    }

}
