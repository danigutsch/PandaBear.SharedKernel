using Microsoft.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>Tests outbox reads through an independently configured context.</summary>
/// <param name="options">The read context options.</param>
/// <param name="configurations">The model configurations registered for the read context.</param>
internal sealed class OutboxHealthReadDbContext(
    DbContextOptions<OutboxHealthReadDbContext> options,
    IEnumerable<IDbContextConfiguration<OutboxHealthReadDbContext>> configurations) : DbContext(options)
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
