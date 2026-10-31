using Microsoft.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Context that carries only the disposition hold model contributed by its configurations.
/// </summary>
internal sealed class DispositionHoldTestDbContext(
    DbContextOptions<DispositionHoldTestDbContext> options,
    IEnumerable<IDbContextConfiguration<DispositionHoldTestDbContext>> configurations) : DbContext(options)
{
    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        foreach (var configuration in configurations)
        {
            configuration.ConfigureModel(modelBuilder);
        }
    }
}
