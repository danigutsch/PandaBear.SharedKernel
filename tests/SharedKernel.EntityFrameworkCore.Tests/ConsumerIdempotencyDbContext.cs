using Microsoft.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

internal sealed class ConsumerIdempotencyDbContext(
    DbContextOptions<ConsumerIdempotencyDbContext> options,
    IEnumerable<IDbContextConfiguration<ConsumerIdempotencyDbContext>> configurations) : DbContext(options)
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
