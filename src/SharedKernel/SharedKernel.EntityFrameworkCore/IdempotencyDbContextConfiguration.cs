using Microsoft.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore;

/// <summary>
/// Adds idempotency model configuration to a DbContext.
/// </summary>
/// <typeparam name="TContext">The DbContext type.</typeparam>
internal sealed class IdempotencyDbContextConfiguration<TContext> : IDbContextConfiguration<TContext>
    where TContext : DbContext
{
    /// <inheritdoc />
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfiguration(new IdempotencyEntryEntityConfiguration());
    }
}
