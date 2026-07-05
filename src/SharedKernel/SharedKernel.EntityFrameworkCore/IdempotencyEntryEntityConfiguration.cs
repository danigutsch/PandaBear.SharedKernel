using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SharedKernel.EntityFrameworkCore;

/// <summary>
/// Configures persisted idempotency entries.
/// </summary>
internal sealed class IdempotencyEntryEntityConfiguration : IEntityTypeConfiguration<IdempotencyEntryEntity>
{
    private const string Schema = "messaging";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IdempotencyEntryEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("idempotency_keys", Schema);
        builder.HasKey(entry => new { entry.Scope, entry.Key });
        builder.Property(entry => entry.Scope).HasMaxLength(200).IsRequired();
        builder.Property(entry => entry.Key).HasMaxLength(255).IsRequired();
        builder.Property(entry => entry.State).HasConversion<string>().HasMaxLength(32).IsRequired().IsConcurrencyToken();
        builder.Property(entry => entry.StartedAt).IsConcurrencyToken();
        builder.Property(entry => entry.ResultFingerprint).HasMaxLength(512);
    }
}
