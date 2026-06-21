namespace SharedKernel.Domain.Tests;

internal sealed class TestAggregate(int id) : AggregateRoot<int>(id)
{
    public void Raise(string name)
    {
        AddDomainEvent(new TestDomainEvent(name));
    }
}
