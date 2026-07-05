using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Configures the durable integration event outbox message table.
/// </summary>
internal sealed class IntegrationEventOutboxMessageEntityConfiguration : IEntityTypeConfiguration<IntegrationEventOutboxMessageEntity>
{
    private const string Schema = "messaging";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IntegrationEventOutboxMessageEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("outbox_messages", Schema);
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.HasIndex(message => new { message.PublishedAt, message.EnqueuedAt });
        builder.OwnsOne(message => message.Envelope, envelope =>
        {
            envelope.Property<Guid>("IntegrationEventOutboxMessageEntityId").HasColumnName("Id");
            envelope.Property(value => value.EventId).HasColumnName("EventId");
            envelope.Property(value => value.EventType).HasColumnName("EventType").HasMaxLength(EventEnvelope.EventTypeMaxLength).IsRequired();
            envelope.Property(value => value.EventVersion).HasColumnName("EventVersion");
            envelope.Property(value => value.OccurredAt).HasColumnName("OccurredAt");
            envelope.Property(value => value.PayloadJson).HasColumnName("PayloadJson").IsRequired();
            envelope.HasIndex(value => value.EventId).IsUnique();
        });
        builder.Navigation(message => message.Envelope).IsRequired();
    }
}
