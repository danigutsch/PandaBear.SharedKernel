using PandaBear.Testing;

namespace PandaBear.EntityFrameworkCore.Tests;

[Trait(SharedKernelTestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.IntegrationEventTransportCapability)]
public sealed class FluentMessagingRegistrationTests
{
    [Fact]
    public void Consumer_registration_does_not_start_background_delivery_implicitly()
    {
        // Act
        var count = FluentMessagingRegistrationScenario.CountHostedConsumers(hosted: false);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public void Explicit_hosted_consumer_registration_is_idempotent()
    {
        // Act
        var count = FluentMessagingRegistrationScenario.CountHostedConsumers(hosted: true);

        // Assert
        count.ShouldBe(1);
    }

    [Fact]
    public void Legacy_consumer_registration_retains_automatic_hosted_delivery()
    {
        // Act
        var count = FluentMessagingRegistrationScenario.CountLegacyHostedConsumers();

        // Assert
        count.ShouldBe(1);
    }

    [Fact]
    public void Hosted_delivery_cannot_use_another_contexts_consumer()
    {
        // Act
        Action register = FluentMessagingRegistrationScenario.HostAnotherContextsConsumer;

        // Assert
        _ = register.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void Hosted_delivery_requires_a_registered_consumer()
    {
        // Act
        Action register = FluentMessagingRegistrationScenario.HostWithoutConsumer;

        // Assert
        _ = register.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void Competing_consumer_queues_for_one_context_are_rejected()
    {
        // Act
        Action register = FluentMessagingRegistrationScenario.RegisterCompetingConsumers;

        // Assert
        _ = register.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void Competing_producer_destinations_for_one_context_are_rejected()
    {
        // Act
        Action register = FluentMessagingRegistrationScenario.RegisterCompetingProducers;

        // Assert
        _ = register.ShouldThrow<InvalidOperationException>();
    }
}
