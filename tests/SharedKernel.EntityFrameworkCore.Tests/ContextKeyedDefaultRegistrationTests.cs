using PandaBear.Testing;

namespace PandaBear.EntityFrameworkCore.Tests;

[Trait(SharedKernelTestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.IntegrationEventTransportCapability)]
public sealed class ContextKeyedDefaultRegistrationTests
{
    [Fact]
    public async Task The_unkeyed_outbox_requires_an_explicit_default_when_contexts_share_a_host()
    {
        // Arrange
        await using var scenario = ContextQualifiedMessagingScenario.Create();

        // Act
        var resolve = scenario.ResolveDefaultOutbox;

        // Assert
        var exception = resolve.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldContain(nameof(FirstMessagingDbContext), StringComparison.Ordinal);
        exception.Message.ShouldContain(nameof(SecondMessagingDbContext), StringComparison.Ordinal);
        exception.Message.ShouldContain("UseDefaultIntegrationEventOutbox", StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_unkeyed_idempotency_store_requires_an_explicit_default_when_contexts_share_a_host()
    {
        // Arrange
        await using var scenario = ContextQualifiedMessagingScenario.Create();

        // Act
        var resolve = scenario.ResolveDefaultIdempotencyStore;

        // Assert
        var exception = resolve.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldContain("UseDefaultIdempotencyStore", StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_explicit_default_selects_its_context_even_when_registered_second()
    {
        // Arrange
        await using var scenario = ContextQualifiedMessagingScenario.CreateWithDefaultsFor<SecondMessagingDbContext>();

        // Act
        var defaults = scenario.ResolveDefaultContexts();

        // Assert
        defaults.Outbox.ShouldBe(typeof(SecondMessagingDbContext));
        defaults.IdempotencyStore.ShouldBe(typeof(SecondMessagingDbContext));
    }
}
