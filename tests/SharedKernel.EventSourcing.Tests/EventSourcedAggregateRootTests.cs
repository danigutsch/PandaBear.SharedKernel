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
        (aggregate.Name).ShouldBe("Rota Romantica");
        (aggregate.Version).ShouldBe(2);
        (aggregate.GetUncommittedEvents()).ShouldBeEmpty();
    }

    [Fact]
    public void Replay_rejects_null_event_without_advancing_version()
    {
        // Arrange
        var aggregate = new TestAggregate("tour-1");
        var events = new object?[] { null }.Cast<object>();

        // Act, Assert
        ((Action)(() => aggregate.Replay(events))).ShouldThrow<ArgumentNullException>();
        (aggregate.Version).ShouldBe(0);
        (aggregate.GetUncommittedEvents()).ShouldBeEmpty();
    }

    [Fact]
    public void AddEvent_applies_and_tracks_uncommitted_event()
    {
        // Arrange
        var aggregate = new TestAggregate("tour-1");

        // Act
        aggregate.ChangeName("Rota Romantica");

        // Assert
        var uncommittedEvent = (aggregate.GetUncommittedEvents()).ShouldHaveSingleItem();
        (uncommittedEvent).ShouldBeOfType<NameChanged>();
        (aggregate.Name).ShouldBe("Rota Romantica");
        (aggregate.Version).ShouldBe(1);
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
        (aggregate.GetUncommittedEvents()).ShouldBeEmpty();
        (aggregate.Version).ShouldBe(1);
    }

}
