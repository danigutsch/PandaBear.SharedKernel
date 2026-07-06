using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.EntityFrameworkCore;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Configures the durable integration event outbox message table.
/// </summary>
internal sealed class IntegrationEventOutboxMessageConfiguration : IEntityTypeConfiguration<IntegrationEventOutboxMessage>
{
    private const string TableName = "outbox_messages";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IntegrationEventOutboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(TableName, SharedKernelSchemas.Messaging);
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.HasIndex(message => new { message.PublishedAt, message.EnqueuedAt });
        builder.HasIndex(message => new { message.PublishedAt, message.NextPublishAttemptAt, message.EnqueuedAt });
        builder.Property(message => message.EnvelopeSpec).HasMaxLength(100).IsRequired();
        builder.Property(message => message.EnvelopeSpecVersion).HasMaxLength(20).IsRequired();
        builder.Property(message => message.EventId).HasMaxLength(200).IsRequired();
        builder.HasIndex(message => message.EventId).IsUnique();
        builder.Property(message => message.Source).HasConversion(static uri => uri.ToString(), static value => new Uri(value)).HasMaxLength(2048).IsRequired();
        builder.Property(message => message.EventType).HasMaxLength(EventEnvelope.EventTypeMaxLength).IsRequired();
        builder.Property(message => message.Subject).HasMaxLength(500);
        builder.Property(message => message.DataContentType).HasMaxLength(200);
        builder.Property(message => message.DataSchema).HasConversion(static uri => uri == null ? null : uri.ToString(), static value => value == null ? null : new Uri(value)).HasMaxLength(2048);
        builder.Property(message => message.Payload);
        builder.Property(message => message.PayloadEncoding).HasConversion<string>().HasMaxLength(20);
        builder.Property(message => message.ExtensionAttributesJson);
        builder.Property(message => message.LastPublishError).HasMaxLength(IntegrationEventOutboxMessage.LastPublishErrorMaxLength);
    }
}
