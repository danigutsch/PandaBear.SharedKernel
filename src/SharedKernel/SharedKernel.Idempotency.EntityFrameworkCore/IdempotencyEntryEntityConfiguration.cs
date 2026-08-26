using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.EntityFrameworkCore;

namespace SharedKernel.Idempotency.EntityFrameworkCore;

/// <summary>
/// Configures persisted idempotency entries.
/// </summary>
internal sealed class IdempotencyEntryEntityConfiguration : IEntityTypeConfiguration<IdempotencyEntryEntity>
{
    private readonly string schema;
    private readonly string tableName;

    public IdempotencyEntryEntityConfiguration()
        : this(SharedKernelSchemas.Messaging, IdempotencyStorageOptions.DefaultTableName)
    {
    }

    public IdempotencyEntryEntityConfiguration(string schema, string tableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        this.schema = schema;
        this.tableName = tableName;
    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IdempotencyEntryEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(tableName, schema);
        builder.HasKey(entry => new { entry.Scope, entry.Key });
        builder.Property(entry => entry.Scope).HasMaxLength(200).IsRequired();
        builder.Property(entry => entry.Key).HasMaxLength(255).IsRequired();
        builder.Property(entry => entry.State).HasConversion<string>().HasMaxLength(32).IsRequired().IsConcurrencyToken();
        builder.Property(entry => entry.StartedAt).IsConcurrencyToken();
        builder.Property(entry => entry.ResultFingerprint).HasMaxLength(512);
    }
}
