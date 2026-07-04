using Microsoft.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore;

/// <summary>
/// Enables EF Core diagnostics intended for local development only.
/// </summary>
/// <typeparam name="TContext">The DbContext type being configured.</typeparam>
public sealed class DevelopmentDiagnosticsOptionsConfiguration<TContext> : IDbContextOptionsConfiguration<TContext>
    where TContext : DbContext
{
    /// <inheritdoc />
    public void Configure(DbContextOptionsBuilder options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.EnableDetailedErrors();
        options.EnableSensitiveDataLogging();
    }
}
