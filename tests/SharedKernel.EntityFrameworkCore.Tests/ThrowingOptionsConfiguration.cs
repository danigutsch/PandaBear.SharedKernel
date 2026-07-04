using Microsoft.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore.Tests;

internal sealed class ThrowingOptionsConfiguration : IDbContextOptionsConfiguration<TestDbContext>
{
    public void Configure(DbContextOptionsBuilder options) => throw new InvalidOperationException("Should not be instantiated by apply.");
}
