using Microsoft.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Sqlite.Tests;

/// <summary>A context whose model maps the search fold function.</summary>
/// <param name="options">The options.</param>
public sealed class PlaceDbContext(DbContextOptions<PlaceDbContext> options) : DbContext(options)
{
    /// <summary>Gets the places.</summary>
    public DbSet<Place> Places => Set<Place>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasSearchFoldFunction();
        modelBuilder.Entity<Place>(place =>
        {
            place.HasKey(entity => entity.Id);
            place.Property(entity => entity.Code).HasConversion(code => code.Value, text => new PlaceCode(text));
        });
    }
}
