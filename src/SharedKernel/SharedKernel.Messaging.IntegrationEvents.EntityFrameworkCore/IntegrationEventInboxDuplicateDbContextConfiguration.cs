using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PandaBear.EntityFrameworkCore;
using PandaBear.Idempotency.EntityFrameworkCore;

namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>Maps optional observations alongside the inbox table with the same retention lifetime.</summary>
/// <typeparam name="TContext">The context that owns the inbox.</typeparam>
internal sealed class IntegrationEventInboxDuplicateDbContextConfiguration<TContext>(
    IOptions<IntegrationEventInboxDuplicateOptions<TContext>> options) : IDbContextConfiguration<TContext>
    where TContext : DbContext
{
    /// <inheritdoc />
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        var parent = modelBuilder.Entity<IdempotencyEntryEntity>().Metadata;
        var tableName = options.Value.TableName;
        if (string.Equals(tableName, parent.GetTableName(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The inbox and duplicate observations require distinct tables.");
        }

        var entity = modelBuilder.Entity<IntegrationEventInboxDuplicateObservation>();
        entity.ToTable(tableName, parent.GetSchema());
        entity.HasKey(row => new { row.Scope, row.Key });
        entity.Property(row => row.ConsumerName).HasMaxLength(IntegrationEventTransportMessage.ConsumerNameMaxLength);
        entity.HasIndex(row => new { row.ConsumerName, row.EventId }).IsUnique();
        entity.HasIndex(row => new { row.ConsumerName, row.LastObservedAt, row.EventId }).IsDescending(false, true, true);
        entity.HasOne<IdempotencyEntryEntity>().WithOne()
            .HasForeignKey<IntegrationEventInboxDuplicateObservation>(row => new { row.Scope, row.Key })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
