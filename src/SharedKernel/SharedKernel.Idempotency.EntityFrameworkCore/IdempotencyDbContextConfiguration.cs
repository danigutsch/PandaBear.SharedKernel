using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharedKernel.EntityFrameworkCore;

namespace SharedKernel.Idempotency.EntityFrameworkCore;

/// <summary>
/// Adds idempotency model configuration to a DbContext.
/// </summary>
/// <typeparam name="TContext">The DbContext type.</typeparam>
internal sealed class IdempotencyDbContextConfiguration<TContext> : IDbContextConfiguration<TContext>
    where TContext : DbContext
{
    private readonly IOptionsMonitor<IdempotencyStorageOptions>? storageOptions;

    public IdempotencyDbContextConfiguration()
    {
    }

    public IdempotencyDbContextConfiguration(IOptionsMonitor<IdempotencyStorageOptions> storageOptions)
    {
        ArgumentNullException.ThrowIfNull(storageOptions);

        this.storageOptions = storageOptions;
    }

    /// <inheritdoc />
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        var options = storageOptions?.Get(IdempotencyOptionsNames.Storage<TContext>())
            ?? new IdempotencyStorageOptions();
        modelBuilder.ApplyConfiguration(new IdempotencyEntryEntityConfiguration(options.Schema, options.TableName));
    }
}
