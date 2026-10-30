using Microsoft.EntityFrameworkCore;

namespace PandaBear.Testing.EntityFrameworkCore;

/// <summary>A minimal context exposing a set of <see cref="TestEntity" />, usable as a transaction target without touching the set.</summary>
/// <param name="options">The EF Core options used to configure the context.</param>
public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    /// <summary>Gets the set of test entities stored by the context.</summary>
    public DbSet<TestEntity> Entities => Set<TestEntity>();
}
