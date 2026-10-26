using Microsoft.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore;

/// <summary>
/// Baseline for a write-side <typeparamref name="TContext" /> that sets the default query tracking behavior to
/// <see cref="QueryTrackingBehavior.TrackAll" />.
/// </summary>
/// <remarks>
/// Register it with <see cref="DbContextConfigurationServiceCollectionExtensions.AddDbContextConfiguration{TContext}" />
/// before the context's own configurations. It sets no provider, schema, or model option, so application policy stays
/// in the application's own <see cref="IDbContextConfiguration{TContext}" />. Individual queries can still call
/// <c>AsTracking</c> or <c>AsNoTracking</c>.
/// </remarks>
/// <typeparam name="TContext">The DbContext type the baseline configures.</typeparam>
public sealed class WriteDbContextConfiguration<TContext> : IDbContextConfiguration<TContext>
    where TContext : DbContext
{
    /// <inheritdoc />
    public void ConfigureOptions(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        _ = optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);
    }
}
