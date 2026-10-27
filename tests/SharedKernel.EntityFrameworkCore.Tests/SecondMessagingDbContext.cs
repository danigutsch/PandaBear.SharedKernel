using Microsoft.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// In-memory test context that maps SecondMessagingBusinessRecord to second_messaging.second_records and applies every supplied model configuration.
/// </summary>
/// <param name="options">The EF Core options used to configure the context.</param>
/// <param name="configurations">Model configurations applied in OnModelCreating.</param>
internal sealed class SecondMessagingDbContext(
    DbContextOptions<SecondMessagingDbContext> options,
    IEnumerable<IDbContextConfiguration<SecondMessagingDbContext>> configurations) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<SecondMessagingBusinessRecord>(builder =>
        {
            builder.ToTable("second_records", "second_messaging");
            builder.HasKey(record => record.Id);
            builder.Property(record => record.Id).ValueGeneratedNever();
        });

        foreach (var configuration in configurations)
        {
            configuration.ConfigureModel(modelBuilder);
        }
    }
}
