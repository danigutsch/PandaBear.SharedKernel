using Microsoft.EntityFrameworkCore;
using PandaBear.Idempotency.EntityFrameworkCore;
using PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Tests that the parameterless storage configurations, which <c>ApplyConfigurationsFromAssembly</c> can create, map to the default
/// <c>messaging</c> schema and table names.
/// </summary>
[Trait(SharedKernelTestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
public sealed class DefaultStorageMappingTests
{
    [Fact]
    [Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.IdempotencyCapability)]
    public void The_default_idempotency_configuration_maps_to_the_messaging_schema()
    {
        // Arrange
        var model = new ModelBuilder();

        // Act
        model.ApplyConfiguration(new IdempotencyEntryEntityConfiguration());

        // Assert
        var entity = model.Model.FindEntityType(typeof(IdempotencyEntryEntity)).ShouldNotBeNull();
        entity.GetSchema().ShouldBe("messaging");
        entity.GetTableName().ShouldBe("idempotency_keys");
    }

    [Fact]
    [Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.IntegrationEventTransportCapability)]
    public void The_default_outbox_configuration_maps_to_the_messaging_schema()
    {
        // Arrange
        var model = new ModelBuilder();

        // Act
        model.ApplyConfiguration(new IntegrationEventOutboxMessageConfiguration());

        // Assert
        var entity = model.Model.FindEntityType(typeof(IntegrationEventOutboxMessage)).ShouldNotBeNull();
        entity.GetSchema().ShouldBe("messaging");
        entity.GetTableName().ShouldBe("outbox_messages");
    }

    [Fact]
    [Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.IntegrationEventTransportCapability)]
    public void The_default_transport_configuration_maps_to_the_messaging_schema_and_takes_part_in_migrations()
    {
        // Arrange
        var model = new ModelBuilder();

        // Act
        model.ApplyConfiguration(new IntegrationEventTransportMessageConfiguration());

        // Assert
        var entity = model.Model.FindEntityType(typeof(IntegrationEventTransportMessage)).ShouldNotBeNull();
        entity.GetSchema().ShouldBe("messaging");
        entity.GetTableName().ShouldBe("transport_messages");
        entity.IsTableExcludedFromMigrations().ShouldBeFalse();
    }
}
