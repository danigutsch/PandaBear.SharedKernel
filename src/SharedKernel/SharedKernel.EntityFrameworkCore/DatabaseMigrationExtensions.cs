using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.EntityFrameworkCore;

/// <summary>Applies the migrations of a context at start-up.</summary>
public static class DatabaseMigrationExtensions
{
    /// <summary>
    /// Applies every pending migration of <typeparamref name="TContext"/> in a new service scope. A database that is already up to date is left unchanged.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="services">The root service provider.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when the database schema is up to date.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    [RequiresDynamicCode("EF Core migrations use runtime code generation and are not Native AOT compatible.")]
    [RequiresUnreferencedCode("EF Core migrations use reflection over the migrations assembly and are not trimming compatible.")]
    public static async Task ApplyMigrations<TContext>(this IServiceProvider services, CancellationToken ct = default)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        var scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            await context.Database.MigrateAsync(ct).ConfigureAwait(false);
        }
    }
}
