using PandaBear.Idempotency;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// PostgreSQL tests of the idempotency store under concurrent starts.
/// </summary>
/// <param name="fixture">Shared PostgreSQL fixture that supplies the isolated database.</param>
[Trait(SharedKernelTestTraitNames.CategoryName, TestTraits.DatabaseIntegrationCategory)]
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.IdempotencyCapability)]
public sealed class PostgreSqlIdempotencyStoreTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private PostgreSqlIdempotencyStoreScenario? scenario;

    private PostgreSqlIdempotencyStoreScenario Scenario =>
        scenario ?? throw new InvalidOperationException("Test scenario is not initialized.");

    /// <summary>
    /// Creates the PostgreSQL idempotency scenario for the test.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        scenario = await PostgreSqlIdempotencyStoreScenario.Create(fixture, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Disposes the scenario created for the test, if any.
    /// </summary>
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
