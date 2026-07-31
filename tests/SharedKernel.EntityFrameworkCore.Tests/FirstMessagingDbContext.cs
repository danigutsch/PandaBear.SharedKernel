using Microsoft.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore.Tests;

internal sealed class FirstMessagingDbContext(
    DbContextOptions<FirstMessagingDbContext> options,
    IEnumerable<IDbContextConfiguration<FirstMessagingDbContext>> configurations) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<FirstMessagingBusinessRecord>(builder =>
        {
            builder.ToTable("first_records", "first_messaging");
            builder.HasKey(record => record.Id);
            builder.Property(record => record.Id).ValueGeneratedNever();
        });

        foreach (var configuration in configurations)
        {
            configuration.ConfigureModel(modelBuilder);
        }
    }
}
