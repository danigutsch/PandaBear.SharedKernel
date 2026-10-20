using Microsoft.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

internal sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<TestEntity> Entities => Set<TestEntity>();
}
