using SharedKernel.Idempotency;
using SharedKernel.Testing;

namespace SharedKernel.EntityFrameworkCore.Tests;

[Trait(SharedKernelTestTraitNames.CategoryName, TestTraits.DatabaseIntegrationCategory)]
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.IdempotencyCapability)]
public sealed class PostgreSqlIdempotencyStoreTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private PostgreSqlIdempotencyStoreScenario? scenario;

    private PostgreSqlIdempotencyStoreScenario Scenario =>
        scenario ?? throw new InvalidOperationException("Test scenario is not initialized.");

    public async ValueTask InitializeAsync()
    {
        scenario = await PostgreSqlIdempotencyStoreScenario.Create(fixture, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        var currentScenario = scenario;
        scenario = null;

        if (currentScenario is not null)
        {
            await currentScenario.DisposeAsync();
        }
    }

    [Fact]
    public async Task Concurrent_starts_have_exactly_one_owner_and_one_started_duplicate()
    {
        // Act
        var results = await Scenario.TryStartConcurrently(TestContext.Current.CancellationToken);

        // Assert
        results.Count(static result => result.Started).ShouldBe(1);
        var duplicate = results.ShouldHaveSingleItem(static result => !result.Started);
        duplicate.ExistingEntry.ShouldNotBeNull().State.ShouldBe(IdempotencyEntryState.Started);
        var rowCount = await Scenario.CountEntries(TestContext.Current.CancellationToken);
        rowCount.ShouldBe(1);
    }
}
