using Microsoft.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore.Tests;

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
