namespace SharedKernel.EventSourcing.Tests;

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
