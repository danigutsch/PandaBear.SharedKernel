using Microsoft.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

internal sealed class ConsumerOutboxDbContext(
    DbContextOptions<ConsumerOutboxDbContext> options,
    IEnumerable<IDbContextConfiguration<ConsumerOutboxDbContext>> configurations) : DbContext(options)
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
