using Microsoft.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore.Tests;

internal sealed class ThrowingDbContextConfiguration : IDbContextConfiguration<TestDbContext>
{
    public void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        throw new InvalidOperationException("Should not be instantiated by apply.");

    public void ConfigureOptions(DbContextOptionsBuilder optionsBuilder) =>
        throw new InvalidOperationException("Should not be instantiated by apply.");

    public void ConfigureModel(ModelBuilder modelBuilder) =>
        throw new InvalidOperationException("Should not be instantiated by apply.");
}
