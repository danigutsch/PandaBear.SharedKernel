using Microsoft.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Sqlite.Tests;

/// <summary>A context whose schema comes only from the hand-written <see cref="CreateLedger"/> migration.</summary>
/// <param name="options">The options.</param>
public sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options)
{
    /// <summary>Gets the entries.</summary>
    public DbSet<LedgerEntry> Entries => Set<LedgerEntry>();

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        // The test migration has no model snapshot, so the pending-model-changes check would reject it.
        optionsBuilder.ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<LedgerEntry>(entry =>
        {
            entry.ToTable("Entries");
            entry.HasKey(item => item.Id);
        });
    }
}
