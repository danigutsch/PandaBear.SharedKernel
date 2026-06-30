namespace SharedKernel.Domain.Tests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.AggregateRootCapability)]
[Trait(Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
public sealed class AggregateRootIdentityTests
{
    [Fact]
    public void Aggregate_root_exposes_identity_and_domain_event_behavior_through_interfaces()
    {
        // Arrange
        const int expectedId = 42;
        var aggregate = new TestAggregate(expectedId);

        // Act
        var aggregateRootId = ((IAggregateRoot<int>)aggregate).Id;
        var domainEvents = ((IAggregateRoot<int>)aggregate).GetDomainEvents();

        // Assert
        Assert.Equal(expectedId, aggregateRootId);
        Assert.Empty(domainEvents);
    }

    [Fact]
    public void Aggregate_root_records_and_clears_domain_events_through_generic_interface()
    {
        // Arrange
        var aggregate = new TestAggregate(42);

        // Act
        aggregate.Raise("tour-created");
        var recordedEvents = ((IAggregateRoot<int>)aggregate).GetDomainEvents();

        // Assert
        var domainEvent = Assert.Single(recordedEvents);
        var typedEvent = Assert.IsType<TestDomainEvent>(domainEvent);
        Assert.Equal("tour-created", typedEvent.Name);

        // Act
        ((IAggregateRoot<int>)aggregate).ClearDomainEvents();

        // Assert
        Assert.Empty(((IAggregateRoot<int>)aggregate).GetDomainEvents());
    }
}
