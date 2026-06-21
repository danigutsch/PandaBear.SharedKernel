namespace SharedKernel.Domain.Tests;

public sealed class DomainPrimitiveTests
{
    [Fact]
    public void Entities_with_the_same_type_and_id_are_equal()
    {
        var first = new TestEntity(42);
        var second = new TestEntity(42);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Entities_with_different_types_are_not_equal_even_when_ids_match()
    {
        var first = new TestEntity(42);
        var second = new OtherTestEntity(42);

        Assert.NotEqual<Entity<int>>(first, second);
    }

    [Fact]
    public void Aggregate_roots_record_and_clear_domain_events()
    {
        var aggregate = new TestAggregate(42);

        aggregate.Raise("tour-created");
        var recordedEvents = aggregate.GetDomainEvents();

        var domainEvent = Assert.Single(recordedEvents);
        var typedEvent = Assert.IsType<TestDomainEvent>(domainEvent);
        Assert.Equal("tour-created", typedEvent.Name);

        aggregate.ClearDomainEvents();

        Assert.Empty(aggregate.GetDomainEvents());
    }
}
