using Microsoft.EntityFrameworkCore;
using SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore.Tests;

/// <summary>A context whose model applies the integration event transport configuration, used to exercise provider guards.</summary>
/// <param name="options">The options that select the database provider under test.</param>
internal sealed class TransportProviderGuardDbContext(DbContextOptions<TransportProviderGuardDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        new IntegrationEventTransportDbContextConfiguration<TransportProviderGuardDbContext>().ConfigureModel(modelBuilder);
    }
}
