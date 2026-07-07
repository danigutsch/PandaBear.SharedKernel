using Microsoft.EntityFrameworkCore;
using SharedKernel.EntityFrameworkCore;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Adds the integration-event transport model to an EF Core context.
/// </summary>
/// <typeparam name="TContext">The DbContext type that owns or reads the transport table.</typeparam>
internal sealed class IntegrationEventTransportDbContextConfiguration<TContext> : IDbContextConfiguration<TContext>
    where TContext : DbContext
{
    /// <inheritdoc />
    public void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
    }

    /// <inheritdoc />
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfiguration(new IntegrationEventTransportMessageConfiguration());
    }
}
