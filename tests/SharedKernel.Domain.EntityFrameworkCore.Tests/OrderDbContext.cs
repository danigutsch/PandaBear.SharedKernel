using Microsoft.EntityFrameworkCore;

namespace PandaBear.Domain.EntityFrameworkCore.Tests;

/// <summary>
/// In-memory test context exposing the Order set.
/// </summary>
/// <param name="options">The EF Core options used to configure the context.</param>
internal sealed class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Set of orders stored by the context.
    /// </summary>
    public DbSet<Order> Orders => Set<Order>();
}
