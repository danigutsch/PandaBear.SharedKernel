using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.EntityFrameworkCore;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Configures the durable integration-event transport table.
/// </summary>
internal sealed class IntegrationEventTransportMessageConfiguration : IEntityTypeConfiguration<IntegrationEventTransportMessage>
{
    private readonly string schema;
    private readonly string tableName;
    private readonly bool excludeFromMigrations;

    public IntegrationEventTransportMessageConfiguration()
        : this(
            SharedKernelSchemas.Messaging,
            IntegrationEventStorageOptions.DefaultTransportTableName,
            excludeFromMigrations: false)
    {
    }

    public IntegrationEventTransportMessageConfiguration(
        string schema,
        string tableName,
        bool excludeFromMigrations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        this.schema = schema;
        this.tableName = tableName;
        this.excludeFromMigrations = excludeFromMigrations;
    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IntegrationEventTransportMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (excludeFromMigrations)
        {
            builder.ToTable(tableName, schema, table => table.ExcludeFromMigrations());
        }
        else
        {
            builder.ToTable(tableName, schema);
        }
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.Property(message => message.ConsumerName).HasMaxLength(IntegrationEventTransportMessage.ConsumerNameMaxLength).IsRequired();
        builder.HasIndex(message => new { message.ConsumerName, message.EventId }).IsUnique();
        builder.HasIndex(message => new { message.ConsumerName, message.ProcessedAt, message.NextConsumeAttemptAt, message.ReceivedAt });
        builder.Property(message => message.EnvelopeSpec).HasMaxLength(100).IsRequired();
        builder.Property(message => message.EnvelopeSpecVersion).HasMaxLength(20).IsRequired();
        builder.Property(message => message.EventId).HasMaxLength(200).IsRequired();
        builder.Property(message => message.Source).HasConversion(static uri => uri.ToString(), static value => new Uri(value)).HasMaxLength(2048).IsRequired();
        builder.Property(message => message.EventType).HasMaxLength(EventEnvelope.EventTypeMaxLength).IsRequired();
        builder.Property(message => message.Subject).HasMaxLength(500);
        builder.Property(message => message.DataContentType).HasMaxLength(200);
        builder.Property(message => message.DataSchema).HasConversion(static uri => uri == null ? null : uri.ToString(), static value => value == null ? null : new Uri(value)).HasMaxLength(2048);
        builder.Property(message => message.Payload);
        builder.Property(message => message.PayloadEncoding).HasConversion<string>().HasMaxLength(20);
        builder.Property(message => message.ExtensionAttributesJson);
        builder.Property(message => message.LastConsumeError).HasMaxLength(IntegrationEventTransportMessage.LastConsumeErrorMaxLength);
        builder.Property(message => message.ClaimedBy).HasMaxLength(IntegrationEventTransportMessage.ClaimOwnerMaxLength);
        builder.Property(message => message.ClaimedUntil).IsConcurrencyToken();
    }
}
