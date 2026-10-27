using Microsoft.EntityFrameworkCore;
using PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;
using PandaBear.Testing;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Tests the provider guard of the PostgreSQL transport claim SQL.
/// </summary>
[Trait(SharedKernelTestTraitNames.CategoryName, TestTraits.ProviderGuardCategory)]
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.IntegrationEventTransportCapability)]
public sealed class PostgreSqlIntegrationEventTransportClaimSqlTests
{
    [Fact]
    public async Task ClaimPending_rejects_non_postgresql_provider_before_executing_sql()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TransportProviderGuardDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString("N"))
            .Options;
        await using var dbContext = new TransportProviderGuardDbContext(options);

        // Act
        Func<Task> claim = async () => _ = await PostgreSqlIntegrationEventTransportClaimSql.ClaimPending(
            dbContext,
            "catalog",
            1,
            TimeProvider.System.GetUtcNow(),
            "worker",
            TimeProvider.System.GetUtcNow().AddMinutes(5),
            TestContext.Current.CancellationToken);

        // Assert
        var exception = await claim.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldContain("Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal);
    }
}
