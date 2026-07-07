using Microsoft.EntityFrameworkCore;
using SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore.Tests;

internal sealed class TransportProviderGuardDbContext(DbContextOptions<TransportProviderGuardDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        new IntegrationEventTransportDbContextConfiguration<TransportProviderGuardDbContext>().ConfigureModel(modelBuilder);
    }
}
