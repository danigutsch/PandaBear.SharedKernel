using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PandaBear.EntityFrameworkCore;

namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Adds the integration-event transport model to an EF Core context.
/// </summary>
/// <typeparam name="TContext">The DbContext type that owns or reads the transport table.</typeparam>
internal sealed class IntegrationEventTransportDbContextConfiguration<TContext> : IDbContextConfiguration<TContext>
    where TContext : DbContext
{
    private readonly IOptionsMonitor<IntegrationEventStorageOptions>? storageOptions;

    /// <summary>Creates a configuration that uses the default integration event storage options.</summary>
    public IntegrationEventTransportDbContextConfiguration()
    {
    }

    /// <summary>
    /// Creates a configuration that reads the context's named integration event storage options.
    /// </summary>
    /// <param name="storageOptions">Monitor that supplies the per-context storage options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="storageOptions"/> is <see langword="null"/>.</exception>
    public IntegrationEventTransportDbContextConfiguration(IOptionsMonitor<IntegrationEventStorageOptions> storageOptions)
    {
        ArgumentNullException.ThrowIfNull(storageOptions);

        this.storageOptions = storageOptions;
    }

    /// <inheritdoc />
    public void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
    }

    /// <inheritdoc />
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        var options = storageOptions?.Get(IntegrationEventOptionsNames.Storage<TContext>())
            ?? new IntegrationEventStorageOptions();
        modelBuilder.ApplyConfiguration(new IntegrationEventTransportMessageConfiguration(
            options.EffectiveTransportSchema,
            options.TransportTableName,
            options.ExcludeTransportFromMigrations));
    }
}
