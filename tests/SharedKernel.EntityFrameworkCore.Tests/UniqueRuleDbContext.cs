using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>A PostgreSQL context with one rule-tagged unique index and one untagged unique index.</summary>
/// <param name="options">The options.</param>
public sealed class UniqueRuleDbContext(DbContextOptions<UniqueRuleDbContext> options) : DbContext(options)
{
    /// <summary>The rule of the email index.</summary>
    public const string EmailRule = "member.email";

    /// <summary>Creates a context over a PostgreSQL data source.</summary>
    /// <param name="dataSource">The data source.</param>
    /// <returns>The context.</returns>
    public static UniqueRuleDbContext Create(NpgsqlDataSource dataSource) =>
        new(new DbContextOptionsBuilder<UniqueRuleDbContext>().UseNpgsql(dataSource).Options);

    /// <summary>Gets the members.</summary>
    public DbSet<UniqueRuleMember> Members => Set<UniqueRuleMember>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<UniqueRuleMember>(member =>
        {
            member.ToTable("unique_rule_members");
            member.HasKey(entity => entity.Id);
            member.HasIndex(entity => entity.Email).HasUniqueRule(EmailRule);
            member.HasIndex(entity => entity.Nickname).IsUnique();
        });
    }
}
