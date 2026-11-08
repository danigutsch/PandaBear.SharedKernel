using Microsoft.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Sqlite.Tests;

/// <summary>A context whose indexes exercise every unique-rule case.</summary>
/// <param name="options">The options.</param>
public sealed class AccountDbContext(DbContextOptions<AccountDbContext> options) : DbContext(options)
{
    /// <summary>The rule of the email index.</summary>
    public const string EmailRule = "account.email";

    /// <summary>The rule of the composite tenant and code index.</summary>
    public const string CodeRule = "account.code";

    /// <summary>Gets the accounts.</summary>
    public DbSet<Account> Accounts => Set<Account>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<Account>(account =>
        {
            account.HasKey(entity => entity.Id);
            account.Property(entity => entity.Id).ValueGeneratedNever();
            account.Property(entity => entity.Name).IsRequired();
            account.HasIndex(entity => entity.Email).HasUniqueRule(EmailRule);
            account.HasIndex(entity => new { entity.TenantId, entity.Code }).HasUniqueRule(CodeRule);
            account.HasIndex(entity => entity.Nickname).IsUnique();
        });
    }
}
