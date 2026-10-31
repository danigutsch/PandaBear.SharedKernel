using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PandaBear.DispositionHold.EntityFrameworkCore;

/// <summary>
/// Maps <see cref="Hold" /> to one table. The scope is stored as one <c>kind/key</c> column, which is unambiguous
/// because scope identifiers never contain a slash. A stored value that is not in that form fails materialization, so
/// a corrupt row makes checks over its scope refuse rather than report clear.
/// </summary>
internal sealed class HoldEntityConfiguration(string schema, string tableName) : IEntityTypeConfiguration<Hold>
{
    private static readonly ValueConverter<DispositionScope, string> ScopeConverter = new(
        scope => scope.ToString(),
        value => ParseScope(value));

    private static DispositionScope ParseScope(string value)
    {
        var separator = value.IndexOf('/', StringComparison.Ordinal);
        return separator < 0
            ? throw new FormatException("A stored hold scope is not in the kind/key form.")
            : DispositionScope.From(value[..separator], value[(separator + 1)..]);
    }

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
