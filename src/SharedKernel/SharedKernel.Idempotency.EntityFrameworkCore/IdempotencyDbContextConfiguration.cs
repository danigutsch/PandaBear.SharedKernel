using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PandaBear.EntityFrameworkCore;

namespace PandaBear.Idempotency.EntityFrameworkCore;

/// <summary>
/// Adds idempotency model configuration to a DbContext.
/// </summary>
/// <typeparam name="TContext">The DbContext type.</typeparam>
internal sealed class IdempotencyDbContextConfiguration<TContext> : IDbContextConfiguration<TContext>
    where TContext : DbContext
{
    private readonly IOptionsMonitor<IdempotencyStorageOptions>? storageOptions;

    /// <summary>Creates a configuration that uses the default idempotency storage options.</summary>
    public IdempotencyDbContextConfiguration()
    {
    }

    /// <summary>
    /// Creates a configuration that reads the context's named idempotency storage options.
    /// </summary>
    /// <param name="storageOptions">Monitor that supplies the per-context storage options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="storageOptions"/> is <see langword="null"/>.</exception>
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
