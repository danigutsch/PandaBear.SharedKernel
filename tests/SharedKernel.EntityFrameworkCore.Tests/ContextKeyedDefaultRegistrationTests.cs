namespace SharedKernel.EntityFrameworkCore.Tests;

/// <summary>
/// Tests that the unkeyed outbox and idempotency store need an explicit default when several messaging contexts share one host.
/// </summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
[Trait(TestTraitNames.CapabilityName, TestTraitValues.IntegrationEventTransport)]
public sealed class ContextKeyedDefaultRegistrationTests
{
    [Fact]
    public async Task Outbox_reader_maps_the_writer_table_without_claiming_migrations_or_a_writer()
    {
        // Arrange
        var expectedWriter = typeof(FirstMessagingDbContext);

        // Act
        var storage = await ContextQualifiedMessagingScenario.InspectOutboxReader();

        // Assert
        storage.DefaultWriter.ShouldBe(expectedWriter);
        storage.ReaderHasWriter.ShouldBeFalse();
        storage.ReaderExcluded.ShouldBeTrue();
        storage.WriterExcluded.ShouldBeFalse();
        storage.ReaderSchema.ShouldBe(storage.WriterSchema);
        storage.ReaderTable.ShouldBe(storage.WriterTable);
    }

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
