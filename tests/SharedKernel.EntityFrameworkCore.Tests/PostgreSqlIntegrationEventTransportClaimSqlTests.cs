using Microsoft.EntityFrameworkCore;
using SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;
using SharedKernel.Testing;

namespace SharedKernel.EntityFrameworkCore.Tests;

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
            DateTimeOffset.UtcNow,
            "worker",
            DateTimeOffset.UtcNow.AddMinutes(5),
            TestContext.Current.CancellationToken);

        // Assert
        var exception = await claim.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldContain("Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal);
    }
}
