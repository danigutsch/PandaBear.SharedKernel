using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PandaBear.DispositionHold.EntityFrameworkCore;

/// <summary>
/// Maps <see cref="Hold" /> to one table. The scope is stored as one <c>kind/key</c> column, which is unambiguous
/// because scope identifiers never contain a slash.
/// </summary>
internal sealed class HoldEntityConfiguration(string schema, string tableName) : IEntityTypeConfiguration<Hold>
{
    private static readonly ValueConverter<DispositionScope, string> ScopeConverter = new(
        scope => scope.ToString(),
        value => DispositionScope.From(value.Substring(0, value.IndexOf('/', StringComparison.Ordinal)), value.Substring(value.IndexOf('/', StringComparison.Ordinal) + 1)));

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Hold> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(tableName, schema);
        builder.HasKey(hold => hold.Id);
        builder.Property(hold => hold.Id).ValueGeneratedNever();
        builder.Property(hold => hold.Scope)
            .HasConversion(ScopeConverter)
            .HasMaxLength(DispositionScope.MaxKindLength + 1 + DispositionScope.MaxKeyLength)
            .IsRequired();
        builder.Property(hold => hold.Basis).HasConversion<string>().HasMaxLength(48).IsRequired();
        builder.Property(hold => hold.PurposeCode).HasMaxLength(Hold.MaxIdentifierLength).IsRequired();
        builder.Property(hold => hold.AuthorityReference).HasMaxLength(Hold.MaxIdentifierLength).IsRequired();
        builder.Property(hold => hold.PlacedBy).HasMaxLength(Hold.MaxIdentifierLength).IsRequired();
        builder.Property(hold => hold.ReleasedBy).HasMaxLength(Hold.MaxIdentifierLength);
        builder.Property(hold => hold.ReleaseReasonCode).HasMaxLength(Hold.MaxIdentifierLength);
        builder.HasIndex(hold => hold.Scope);
    }
}
