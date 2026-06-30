namespace SharedKernel.Domain.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.EntityCapability)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
public sealed class EntityIdentityTests
{
    [Fact]
    public void Entity_exposes_identity_through_domain_interfaces()
    {
        // Arrange
        const int expectedId = 42;
        var entity = new TestEntity(expectedId);

        // Act
        var identifiedId = ((IIdentified<int>)entity).Id;
        var domainEntityId = ((IEntity<int>)entity).Id;

        // Assert
        Assert.Equal(expectedId, identifiedId);
        Assert.Equal(expectedId, domainEntityId);
    }

    [Fact]
    public void Entity_does_not_expose_aggregate_root_behavior()
    {
        // Arrange
        object entity = new TestEntity(42);

        // Act
        var aggregateRoot = entity as IAggregateRoot<int>;

        // Assert
        Assert.Null(aggregateRoot);
    }

    [Fact]
    public void Entity_identity_interfaces_preserve_current_equality_semantics()
    {
        // Arrange
        var first = (IEntity<int>)new TestEntity(42);
        var second = (IEntity<int>)new TestEntity(42);

        // Act
        var areEqual = first.Equals(second);

        // Assert
        Assert.True(areEqual);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }
}
