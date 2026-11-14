using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PandaBear.EntityFrameworkCore.Npgsql;

/// <summary>Registers PostgreSQL support services.</summary>
public static class NpgsqlServiceCollectionExtensions
{
    /// <summary>Registers <see cref="NpgsqlUniqueViolationParser"/> as the <see cref="IUniqueViolationParser"/> unless one is already registered.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddNpgsqlUniqueViolationParser(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IUniqueViolationParser, NpgsqlUniqueViolationParser>();
        return services;
    }
}
