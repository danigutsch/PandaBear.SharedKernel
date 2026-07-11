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
        TestAssert.Equal("Rota Romantica", aggregate.Name);
        TestAssert.Equal(2, aggregate.Version);
        TestAssert.Empty(aggregate.GetUncommittedEvents());
    }

    [Fact]
    public void Replay_rejects_null_event_without_advancing_version()
    {
        // Arrange
        var aggregate = new TestAggregate("tour-1");
        var events = new object?[] { null }.Cast<object>();

        // Act, Assert
        TestAssert.Throws<ArgumentNullException>(() => aggregate.Replay(events));
        TestAssert.Equal(0, aggregate.Version);
        TestAssert.Empty(aggregate.GetUncommittedEvents());
    }

    [Fact]
    public void AddEvent_applies_and_tracks_uncommitted_event()
    {
        // Arrange
        var aggregate = new TestAggregate("tour-1");

        // Act
        aggregate.ChangeName("Rota Romantica");

        // Assert
        var uncommittedEvent = TestAssert.ExactlyOne(aggregate.GetUncommittedEvents());
        TestAssert.IsType<NameChanged>(uncommittedEvent);
        TestAssert.Equal("Rota Romantica", aggregate.Name);
        TestAssert.Equal(1, aggregate.Version);
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
        TestAssert.Empty(aggregate.GetUncommittedEvents());
        TestAssert.Equal(1, aggregate.Version);
    }

}
