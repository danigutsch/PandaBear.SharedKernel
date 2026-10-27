using Microsoft.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Minimal test context exposing a set of TestEntity.
/// </summary>
/// <param name="options">The EF Core options used to configure the context.</param>
internal sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Set of test entities stored by the context.
    /// </summary>
    public DbSet<TestEntity> Entities => Set<TestEntity>();
}
