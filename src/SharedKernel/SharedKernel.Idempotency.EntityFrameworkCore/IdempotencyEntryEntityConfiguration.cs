using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PandaBear.EntityFrameworkCore;

namespace PandaBear.Idempotency.EntityFrameworkCore;

/// <summary>
/// Configures persisted idempotency entries.
/// </summary>
internal sealed class IdempotencyEntryEntityConfiguration : IEntityTypeConfiguration<IdempotencyEntryEntity>
{
    private readonly string schema;
    private readonly string tableName;

    /// <summary>Creates a configuration that maps to the default schema and table.</summary>
    public IdempotencyEntryEntityConfiguration()
        : this(SharedKernelSchemas.Messaging, IdempotencyStorageOptions.DefaultTableName)
    {
    }

    /// <summary>
    /// Creates a configuration that maps to the given schema and table.
    /// </summary>
    /// <param name="schema">Schema that holds the table.</param>
    /// <param name="tableName">Name of the idempotency table.</param>
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
        builder.Property(entry => entry.RetentionOwner).HasMaxLength(RetentionOwnerIdentifier.MaxLength);
        builder.HasIndex(entry => entry.RetentionOwner);
    }
}
