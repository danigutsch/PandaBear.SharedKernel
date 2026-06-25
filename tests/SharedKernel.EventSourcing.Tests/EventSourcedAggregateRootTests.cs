namespace SharedKernel.EventSourcing.Tests;

public sealed class EventSourcedAggregateRootTests
{
    [Fact]
    public void Replay_Applies_Events_Without_Tracking_Uncommitted_Events()
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
    public void Replay_Rejects_Null_Event_Without_Advancing_Version()
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
    public void AddEvent_Applies_And_Tracks_Uncommitted_Event()
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
    public void ClearUncommittedEvents_Removes_Tracked_Events_Without_Changing_Version()
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

    internal sealed record NameChanged(string Name);

    internal sealed class TestAggregate(string id) : EventSourcedAggregateRoot<string>
    {
        public override string Id { get; } = id;

        public string? Name { get; private set; }

        public void ChangeName(string name) => AddEvent(new NameChanged(name));

        protected override void ApplyEvent(object domainEvent)
        {
            if (domainEvent is NameChanged nameChanged)
            {
                Name = nameChanged.Name;
            }
        }
    }
}
