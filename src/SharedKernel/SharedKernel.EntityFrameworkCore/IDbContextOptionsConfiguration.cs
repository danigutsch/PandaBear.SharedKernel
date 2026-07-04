using Microsoft.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore;

/// <summary>
/// Configures EF Core options for a DbContext during provider registration.
/// </summary>
/// <typeparam name="TContext">The DbContext type being configured.</typeparam>
public interface IDbContextOptionsConfiguration<TContext>
    where TContext : DbContext
{
    /// <summary>
    /// Gets the DbContext type this configuration targets.
    /// </summary>
    Type ContextType => typeof(TContext);

    /// <summary>
    /// Applies the configuration to the EF Core options builder.
    /// </summary>
    /// <param name="options">The EF Core options builder.</param>
    void Configure(DbContextOptionsBuilder options);
}
