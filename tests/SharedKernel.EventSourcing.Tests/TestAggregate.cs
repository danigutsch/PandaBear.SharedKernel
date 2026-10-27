namespace PandaBear.EventSourcing.Tests;

/// <summary>A test event-sourced aggregate whose only state is a name set by NameChanged events.</summary>
/// <param name="id">The aggregate identifier.</param>
internal sealed class TestAggregate(string id) : EventSourcedAggregateRoot<string>
{
    public override string Id { get; } = id;

    /// <summary>Gets the current name, or null until a NameChanged event is applied.</summary>
    public string? Name
    {
        get; private set;
    }

    /// <summary>Raises a NameChanged event with the given name, which applies it and records it as uncommitted.</summary>
    public void ChangeName(string name) => AddEvent(new NameChanged(name));

    protected override void ApplyEvent(object domainEvent)
    {
        if (domainEvent is NameChanged nameChanged)
        {
            Name = nameChanged.Name;
        }
    }
}
