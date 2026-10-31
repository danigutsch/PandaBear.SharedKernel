using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PandaBear.EntityFrameworkCore;

namespace PandaBear.DispositionHold.EntityFrameworkCore;

/// <summary>Adds the hold model to a DbContext.</summary>
/// <typeparam name="TContext">The DbContext type.</typeparam>
/// <param name="storageOptions">Monitor that supplies the per-context storage options.</param>
internal sealed class DispositionHoldDbContextConfiguration<TContext>(IOptionsMonitor<DispositionHoldStorageOptions> storageOptions)
    : IDbContextConfiguration<TContext>
    where TContext : DbContext
{
    /// <inheritdoc />
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        var options = storageOptions.Get(DispositionHoldOptionsNames.Storage<TContext>());
        modelBuilder.ApplyConfiguration(new HoldEntityConfiguration(options.Schema, options.TableName));
    }
}
