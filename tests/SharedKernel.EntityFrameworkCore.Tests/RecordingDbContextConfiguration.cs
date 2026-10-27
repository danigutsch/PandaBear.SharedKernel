using Microsoft.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Configuration for TestDbContext that appends "name-conventions", "name-options" or "name-model" to a shared list when its phase runs.
/// </summary>
/// <param name="calls">List that receives one entry per configured phase.</param>
/// <param name="name">Prefix used in each recorded entry.</param>
internal sealed class RecordingDbContextConfiguration(List<string> calls, string name) : IDbContextConfiguration<TestDbContext>
{
    /// <summary>
    /// Records "name-conventions" after rejecting a null builder.
    /// </summary>
    public void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        calls.Add($"{name}-conventions");
    }

    /// <summary>
    /// Records "name-options" after rejecting a null builder.
    /// </summary>
    public void ConfigureOptions(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        calls.Add($"{name}-options");
    }

    /// <summary>
    /// Records "name-model" after rejecting a null builder.
    /// </summary>
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        calls.Add($"{name}-model");
    }
}
