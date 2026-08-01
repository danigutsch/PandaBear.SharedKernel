using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharedKernel.EntityFrameworkCore;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Adds the integration-event transport model to an EF Core context.
/// </summary>
/// <typeparam name="TContext">The DbContext type that owns or reads the transport table.</typeparam>
internal sealed class IntegrationEventTransportDbContextConfiguration<TContext> : IDbContextConfiguration<TContext>
    where TContext : DbContext
{
    private readonly IOptionsMonitor<IntegrationEventStorageOptions>? storageOptions;

    public IntegrationEventTransportDbContextConfiguration()
    {
    }

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
            options.TransportTableName));
    }
}
