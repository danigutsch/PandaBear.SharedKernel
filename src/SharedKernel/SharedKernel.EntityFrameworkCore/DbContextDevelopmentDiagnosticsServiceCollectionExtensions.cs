using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.EntityFrameworkCore;

/// <summary>
/// Provides DbContext development diagnostics registration helpers.
/// </summary>
public static class DbContextDevelopmentDiagnosticsServiceCollectionExtensions
{
    /// <summary>
    /// Adds EF Core diagnostics intended for local development only.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <typeparam name="TContext">The DbContext type being configured.</typeparam>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddDbContextDevelopmentDiagnostics<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddDbContextConfiguration(new DevelopmentDiagnosticsOptionsConfiguration<TContext>());
    }
}
