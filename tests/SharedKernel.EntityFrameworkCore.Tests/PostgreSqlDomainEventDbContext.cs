using Microsoft.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Test context over PostgreSQL that stores PostgreSqlDomainEventOrder rows.
/// </summary>
/// <param name="options">The EF Core options used to configure the context.</param>
internal sealed class PostgreSqlDomainEventDbContext(DbContextOptions<PostgreSqlDomainEventDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Set of orders stored by the context.
    /// </summary>
    public DbSet<PostgreSqlDomainEventOrder> Orders => Set<PostgreSqlDomainEventOrder>();
}
