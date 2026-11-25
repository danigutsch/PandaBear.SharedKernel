using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.EntityFrameworkCore;

/// <summary>Applies the migrations of a context at start-up.</summary>
public static class DatabaseMigrationExtensions
{
    /// <summary>
    /// Applies every pending migration of <typeparamref name="TContext"/> in a new service scope. A database that is already up to date is left
    /// unchanged. A context on a non-relational provider, such as the in-memory provider of a test host, has no migrations and is skipped.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="services">The root service provider.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when the schema is up to date after migrating; <see langword="false"/> when the provider is not relational and
    /// nothing was done, so follow-up schema work that needs a relational database can be skipped too.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    [RequiresDynamicCode("EF Core migrations use runtime code generation and are not Native AOT compatible.")]
    [RequiresUnreferencedCode("EF Core migrations use reflection over the migrations assembly and are not trimming compatible.")]
    public static async Task<bool> ApplyMigrations<TContext>(this IServiceProvider services, CancellationToken ct = default)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        var scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            if (!context.Database.IsRelational())
            {
                return false;
            }

            await context.Database.MigrateAsync(ct).ConfigureAwait(false);
            return true;
        }
    }

    /// <summary>
    /// Fails when a migration of <typeparamref name="TContext"/> has not been applied, without writing to the database. A host that only reads
    /// a database another host migrates calls it at start-up instead of <see cref="ApplyMigrations{TContext}"/>, so it never runs against an
    /// older schema. It works on a read-only connection; a missing database counts as one without any migration and is not created. A context
    /// on a non-relational provider has no migrations and passes.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="services">The root service provider.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when every migration is applied.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a migration is pending; the message names the context, the count and the first one.</exception>
    [RequiresDynamicCode("EF Core migrations use runtime code generation and are not Native AOT compatible.")]
    [RequiresUnreferencedCode("EF Core migrations use reflection over the migrations assembly and are not trimming compatible.")]
    public static async Task EnsureMigrationsApplied<TContext>(this IServiceProvider services, CancellationToken ct = default)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        var scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            if (!context.Database.IsRelational())
            {
                return;
            }

            IReadOnlyList<string> pending = [.. await context.Database.GetPendingMigrationsAsync(ct).ConfigureAwait(false)];
            if (pending.Count > 0)
            {
                throw new InvalidOperationException(
                    $"The database schema of {typeof(TContext).Name} is missing or out of date ({pending.Count} migration(s) not applied, the "
                    + $"first is {pending[0]}). Apply the migrations from the host that owns the schema before starting this one.");
            }
        }
    }
}
