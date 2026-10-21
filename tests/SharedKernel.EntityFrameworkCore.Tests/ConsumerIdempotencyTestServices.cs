using Microsoft.EntityFrameworkCore;
using PandaBear.Idempotency.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

internal static class ConsumerIdempotencyTestServices
{
    public static ConsumerIdempotencyDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ConsumerIdempotencyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ConsumerIdempotencyDbContext(options, [new IdempotencyDbContextConfiguration<ConsumerIdempotencyDbContext>()]);
    }
}
