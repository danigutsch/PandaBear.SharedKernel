using Microsoft.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>A configuration whose every callback throws, proving that applying configurations must not instantiate or invoke it.</summary>
internal sealed class ThrowingDbContextConfiguration : IDbContextConfiguration<TestDbContext>
{
    /// <summary>Throws an <see cref="InvalidOperationException" /> if the conventions callback is ever invoked.</summary>
    /// <param name="configurationBuilder">The convention builder, which is never used.</param>
    public void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        throw new InvalidOperationException("Should not be instantiated by apply.");

    /// <summary>Throws an <see cref="InvalidOperationException" /> if the options callback is ever invoked.</summary>
    /// <param name="optionsBuilder">The options builder, which is never used.</param>
    public void ConfigureOptions(DbContextOptionsBuilder optionsBuilder) =>
        throw new InvalidOperationException("Should not be instantiated by apply.");

    /// <summary>Throws an <see cref="InvalidOperationException" /> if the model callback is ever invoked.</summary>
    /// <param name="modelBuilder">The model builder, which is never used.</param>
    public void ConfigureModel(ModelBuilder modelBuilder) =>
        throw new InvalidOperationException("Should not be instantiated by apply.");
}
