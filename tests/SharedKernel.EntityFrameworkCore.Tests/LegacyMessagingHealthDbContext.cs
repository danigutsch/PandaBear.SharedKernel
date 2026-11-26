using Microsoft.EntityFrameworkCore;
using SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore.Tests;

/// <summary>Models the outbox and transport before the workflow correlation expansion.</summary>
/// <param name="options">The options for the disposable database.</param>
/// <param name="configurations">The same storage configurations used by the current context.</param>
internal sealed class LegacyMessagingHealthDbContext(
    DbContextOptions<LegacyMessagingHealthDbContext> options,
    IEnumerable<IDbContextConfiguration<LegacyMessagingHealthDbContext>> configurations) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var configuration in configurations)
        {
            configuration.ConfigureModel(modelBuilder);
        }

        modelBuilder.Entity<IntegrationEventOutboxMessage>().Ignore(row => row.WorkflowCorrelationId);
        modelBuilder.Entity<IntegrationEventTransportMessage>().Ignore(row => row.WorkflowCorrelationId);
    }
}
