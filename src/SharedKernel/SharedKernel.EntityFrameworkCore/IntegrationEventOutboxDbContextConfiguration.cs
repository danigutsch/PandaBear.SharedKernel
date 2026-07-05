using Microsoft.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore;

/// <summary>
/// Adds durable integration event outbox model configuration to a DbContext.
/// </summary>
/// <typeparam name="TContext">The DbContext type.</typeparam>
internal sealed class IntegrationEventOutboxDbContextConfiguration<TContext> : IDbContextConfiguration<TContext>
    where TContext : DbContext
{
    /// <inheritdoc />
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfiguration(new IntegrationEventOutboxMessageConfiguration());
    }
}
