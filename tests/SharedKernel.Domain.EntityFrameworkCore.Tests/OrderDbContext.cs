using Microsoft.EntityFrameworkCore;

namespace PandaBear.Domain.EntityFrameworkCore.Tests;

internal sealed class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
}
