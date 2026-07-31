using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharedKernel.EntityFrameworkCore;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Adds durable integration event outbox model configuration to a DbContext.
/// </summary>
/// <typeparam name="TContext">The DbContext type.</typeparam>
internal sealed class IntegrationEventOutboxDbContextConfiguration<TContext> : IDbContextConfiguration<TContext>
    where TContext : DbContext
{
    private readonly IOptionsMonitor<IntegrationEventStorageOptions>? storageOptions;

    public IntegrationEventOutboxDbContextConfiguration()
    {
    }

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
        modelBuilder.ApplyConfiguration(new IntegrationEventOutboxMessageConfiguration(options.Schema, options.OutboxTableName));
    }
}
