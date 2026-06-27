namespace SharedKernel.EventSourcing.Tests;

public sealed class EventSourcedAggregateRootTests
{
    [Fact]
    public void Replay_applies_events_without_tracking_uncommitted_events()
    {
        // Arrange
        var aggregate = new TestAggregate("tour-1");
        var events = new object[]
        {
            new NameChanged("Romantic Road"),
            new NameChanged("Rota Romantica")
        };

        // Act
        aggregate.Replay(events);

        // Assert
        Assert.Equal("Rota Romantica", aggregate.Name);
        Assert.Equal(2, aggregate.Version);
        Assert.Empty(aggregate.GetUncommittedEvents());
    }

    [Fact]
    public void Replay_rejects_null_event_without_advancing_version()
    {
        // Arrange
        var aggregate = new TestAggregate("tour-1");
        var events = new object?[] { null }.Cast<object>();

        // Act, Assert
        Assert.Throws<ArgumentNullException>(() => aggregate.Replay(events));
        Assert.Equal(0, aggregate.Version);
        Assert.Empty(aggregate.GetUncommittedEvents());
    }

    [Fact]
    public void AddEvent_applies_and_tracks_uncommitted_event()
    {
        // Arrange
        var aggregate = new TestAggregate("tour-1");

        // Act
        aggregate.ChangeName("Rota Romantica");

        // Assert
        var uncommittedEvent = Assert.Single(aggregate.GetUncommittedEvents());
        Assert.IsType<NameChanged>(uncommittedEvent);
        Assert.Equal("Rota Romantica", aggregate.Name);
        Assert.Equal(1, aggregate.Version);
    }

    [Fact]
    public void ClearUncommittedEvents_removes_tracked_events_without_changing_version()
    {
        // Arrange
        var aggregate = new TestAggregate("tour-1");
        aggregate.ChangeName("Rota Romantica");

        // Act
        aggregate.ClearUncommittedEvents();

        // Assert
        Assert.Empty(aggregate.GetUncommittedEvents());
        Assert.Equal(1, aggregate.Version);
    }

}
