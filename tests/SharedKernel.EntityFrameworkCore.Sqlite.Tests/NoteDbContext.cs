using Microsoft.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore.Sqlite.Tests;

/// <summary>A context that bumps <see cref="NoteRevision"/> in the same save as every content change.</summary>
/// <param name="options">The options.</param>
public sealed class NoteDbContext(DbContextOptions<NoteDbContext> options) : DbContext(options)
{
    /// <summary>Gets the notes.</summary>
    public DbSet<Note> Notes => Set<Note>();

    /// <summary>Gets the revision.</summary>
    public DbSet<NoteRevision> Revisions => Set<NoteRevision>();

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =>
        this.SaveChangesIncrementingRevision<NoteRevision>(
            revision => revision.Value,
            token => base.SaveChangesAsync(acceptAllChangesOnSuccess, token),
            cancellationToken);

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<Note>(note =>
        {
            note.HasKey(entity => entity.Id);
            note.HasIndex(entity => entity.Text).IsUnique();
        });
        modelBuilder.Entity<NoteRevision>(revision =>
        {
            revision.HasKey(entity => entity.Id);
            revision.Property(entity => entity.Id).ValueGeneratedNever();
            revision.HasData(new NoteRevision { Id = NoteRevision.SingletonId, Value = 0 });
        });
    }
}
