using Microsoft.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// In-memory test context that applies every supplied model configuration, used to host the idempotency model.
/// </summary>
/// <param name="options">The EF Core options used to configure the context.</param>
/// <param name="configurations">Model configurations applied in OnModelCreating.</param>
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
