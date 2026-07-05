using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SharedKernel.EntityFrameworkCore;

/// <summary>
/// Configures the durable integration event outbox message table.
/// </summary>
public sealed class IntegrationEventOutboxMessageConfiguration : IEntityTypeConfiguration<IntegrationEventOutboxMessage>
{
    private const string Schema = "messaging";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IntegrationEventOutboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("outbox_messages", Schema);
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.HasIndex(message => message.EventId).IsUnique();
        builder.HasIndex(message => new { message.PublishedAt, message.EnqueuedAt });
        builder.Property(message => message.EventType).HasMaxLength(200).IsRequired();
        builder.Property(message => message.PayloadJson).IsRequired();
    }
}
