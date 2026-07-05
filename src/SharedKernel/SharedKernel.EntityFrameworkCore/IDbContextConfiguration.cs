using Microsoft.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore;

/// <summary>
/// Configures the three EF Core DbContext configuration phases for a context type.
/// </summary>
/// <typeparam name="TContext">The DbContext type.</typeparam>
public interface IDbContextConfiguration<TContext>
    where TContext : DbContext
{
    /// <summary>
    /// Gets the DbContext type this configuration targets.
    /// </summary>
    Type ContextType => typeof(TContext);

    /// <summary>
    /// Configures model conventions before EF Core builds the model.
    /// </summary>
    /// <param name="configurationBuilder">The convention configuration builder.</param>
    void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
    }

    /// <summary>
    /// Configures context options.
    /// </summary>
    /// <param name="optionsBuilder">The options builder.</param>
    void ConfigureOptions(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
    }

    /// <summary>
    /// Configures the model.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    void ConfigureModel(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
    }
}
