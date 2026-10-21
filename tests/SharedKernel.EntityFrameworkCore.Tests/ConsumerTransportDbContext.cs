using Microsoft.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

internal sealed class ConsumerTransportDbContext(
    DbContextOptions<ConsumerTransportDbContext> options,
    IEnumerable<IDbContextConfiguration<ConsumerTransportDbContext>> configurations) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var configuration in configurations)
        {
            configuration.ConfigureModel(modelBuilder);
        }
    }
}
