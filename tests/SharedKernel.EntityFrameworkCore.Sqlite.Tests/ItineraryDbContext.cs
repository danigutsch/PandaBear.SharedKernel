using Microsoft.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore.Sqlite.Tests;

/// <summary>A context that maps every list of <see cref="Itinerary"/> with <see cref="JsonColumnPropertyBuilderExtensions.HasJsonConversion{T}"/>.</summary>
/// <param name="options">The options.</param>
public sealed class ItineraryDbContext(DbContextOptions<ItineraryDbContext> options) : DbContext(options)
{
    /// <summary>Gets the itineraries.</summary>
    public DbSet<Itinerary> Itineraries => Set<Itinerary>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<Itinerary>(itinerary =>
        {
            itinerary.HasKey(entity => entity.Id);
            itinerary.Property(entity => entity.Cities).HasJsonConversion();
            itinerary.Property(entity => entity.Prices).HasJsonConversion();
            itinerary.Property(entity => entity.ImageIds).HasJsonConversion();
            itinerary.Property(entity => entity.Stops).HasJsonConversion();
            itinerary.Property(entity => entity.Tags!).HasJsonConversion();
        });
    }
}
