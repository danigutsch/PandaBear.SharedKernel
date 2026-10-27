using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PandaBear.EntityFrameworkCore;

namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Adds durable integration event outbox model configuration to a DbContext.
/// </summary>
/// <typeparam name="TContext">The DbContext type.</typeparam>
internal sealed class IntegrationEventOutboxDbContextConfiguration<TContext> : IDbContextConfiguration<TContext>
    where TContext : DbContext
{
    private readonly IOptionsMonitor<IntegrationEventStorageOptions>? storageOptions;

    /// <summary>Creates a configuration that uses the default integration event storage options.</summary>
    public IntegrationEventOutboxDbContextConfiguration()
    {
    }

    /// <summary>
    /// Creates a configuration that reads the context's named integration event storage options.
    /// </summary>
    /// <param name="storageOptions">Monitor that supplies the per-context storage options.</param>
    public IntegrationEventOutboxDbContextConfiguration(IOptionsMonitor<IntegrationEventStorageOptions> storageOptions)
    {
        ArgumentNullException.ThrowIfNull(storageOptions);

        this.storageOptions = storageOptions;
    }

    /// <inheritdoc />
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        var options = storageOptions?.Get(IntegrationEventOptionsNames.Storage<TContext>())
            ?? new IntegrationEventStorageOptions();
        modelBuilder.ApplyConfiguration(new IntegrationEventOutboxMessageConfiguration(
            options.EffectiveOutboxSchema,
            options.OutboxTableName));
    }
}
