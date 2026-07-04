using Microsoft.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore;

/// <summary>
/// Enables EF Core diagnostics intended for local development only.
/// </summary>
/// <typeparam name="TContext">The DbContext type being configured.</typeparam>
public sealed class DevelopmentDiagnosticsOptionsConfiguration<TContext> : IDbContextConfiguration<TContext>
    where TContext : DbContext
{
    /// <inheritdoc />
    public void ConfigureOptions(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        optionsBuilder.EnableDetailedErrors();
        optionsBuilder.EnableSensitiveDataLogging();
    }
}
