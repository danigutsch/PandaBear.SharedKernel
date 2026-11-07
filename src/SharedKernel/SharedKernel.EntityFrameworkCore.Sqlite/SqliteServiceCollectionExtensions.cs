using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PandaBear.EntityFrameworkCore.Sqlite;

/// <summary>Registers SQLite support services.</summary>
public static class SqliteServiceCollectionExtensions
{
    /// <summary>Registers <see cref="SqliteUniqueViolationParser"/> as the <see cref="IUniqueViolationParser"/> unless one is already registered.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddSqliteUniqueViolationParser(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IUniqueViolationParser, SqliteUniqueViolationParser>();
        return services;
    }
}
