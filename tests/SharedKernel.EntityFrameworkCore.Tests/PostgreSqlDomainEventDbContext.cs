using Microsoft.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

internal sealed class PostgreSqlDomainEventDbContext(DbContextOptions<PostgreSqlDomainEventDbContext> options) : DbContext(options)
{
    public DbSet<PostgreSqlDomainEventOrder> Orders => Set<PostgreSqlDomainEventOrder>();
}
