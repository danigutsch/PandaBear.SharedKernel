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
    public void Entities_without_persisted_ids_are_not_equal()
    {
        var first = new TestEntity(0);
        var second = new TestEntity(0);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Entities_with_different_ids_are_not_equal()
    {
        // Arrange
        var first = new TestEntity(42);
        var second = new TestEntity(43);

        // Act
        var areEqual = first.Equals(second);

        // Assert
        Assert.False(areEqual);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Entities_with_default_and_persisted_ids_are_not_equal()
    {
        // Arrange
        var first = new TestEntity(0);
        var second = new TestEntity(42);

        // Act
        var areEqual = first.Equals(second);

        // Assert
        Assert.False(areEqual);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Entity_equality_handles_reference_and_type_comparisons()
    {
        var entity = new TestEntity(42);

        Assert.True(entity.Equals(entity));
        Assert.False(entity.Equals("tour"));
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

    [Fact]
    public void Aggregate_roots_without_events_return_an_empty_snapshot()
    {
        var aggregate = new TestAggregate(42);

        Assert.Empty(aggregate.GetDomainEvents());
    }
}
