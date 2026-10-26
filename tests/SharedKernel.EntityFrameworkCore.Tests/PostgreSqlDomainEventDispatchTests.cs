using PandaBear.Testing;

namespace PandaBear.EntityFrameworkCore.Tests;

[Trait(SharedKernelTestTraitNames.CategoryName, TestTraits.DatabaseIntegrationCategory)]
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.DomainEventDispatchCapability)]
public sealed class PostgreSqlDomainEventDispatchTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Save_changes_dispatches_the_domain_event_and_persists_the_aggregate()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlDomainEventDispatchScenario.Create(fixture, failDispatch: false, ct);

        // Act
        await scenario.SavePlacedOrder(5, ct);

        // Assert
        var persisted = await scenario.CountOrders(ct);
        scenario.Dispatcher.Dispatched.ShouldHaveSingleItem();
        persisted.ShouldBe(1);
    }

    [Fact]
    public async Task A_failing_dispatch_aborts_the_save()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlDomainEventDispatchScenario.Create(fixture, failDispatch: true, ct);

        // Act
        var save = async () => await scenario.SavePlacedOrder(6, ct);

        // Assert
        _ = await save.ShouldThrow<InvalidOperationException>();
        var persisted = await scenario.CountOrders(ct);
        persisted.ShouldBe(0);
    }
}
