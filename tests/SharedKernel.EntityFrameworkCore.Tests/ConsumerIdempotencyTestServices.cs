using Microsoft.EntityFrameworkCore;
using PandaBear.Idempotency.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Creates idempotency test contexts.
/// </summary>
internal static class ConsumerIdempotencyTestServices
{
    /// <summary>
    /// Creates a ConsumerIdempotencyDbContext on a uniquely named in-memory database with the idempotency model configuration applied.
    /// </summary>
    public static ConsumerIdempotencyDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ConsumerIdempotencyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ConsumerIdempotencyDbContext(options, [new IdempotencyDbContextConfiguration<ConsumerIdempotencyDbContext>()]);
    }
}
