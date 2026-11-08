using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.EntityFrameworkCore.Sqlite.Tests;

/// <summary>Tests service registration and the index annotation.</summary>
[Trait("Scope", "Unit")]
[Trait("Capability", "UniqueViolations")]
public sealed class RegistrationAndAnnotationTests
{
    [Fact]
    public void The_sqlite_parser_is_registered_as_the_violation_parser()
    {
        // Arrange
        var services = new ServiceCollection().AddSqliteUniqueViolationParser();

        // Act
        using var provider = services.BuildServiceProvider();

        // Assert
        provider.GetRequiredService<IUniqueViolationParser>().ShouldBeOfType<SqliteUniqueViolationParser>();
    }

    [Fact]
    public void An_existing_parser_registration_is_kept()
    {
        // Arrange
        var custom = new FixedUniqueViolationParser(new UniqueViolation(null, null, []));
        var services = new ServiceCollection().AddSingleton<IUniqueViolationParser>(custom).AddSqliteUniqueViolationParser();

        // Act
        using var provider = services.BuildServiceProvider();

        // Assert
        provider.GetRequiredService<IUniqueViolationParser>().ShouldBeSameAs(custom);
    }

    [Fact]
    public void Registration_requires_a_service_collection()
    {
        // Arrange
        Action act = () => SqliteServiceCollectionExtensions.AddSqliteUniqueViolationParser(null!);

        // Act & Assert
        act.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Tagged_indexes_are_unique_and_carry_their_rule()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        using var context = database.CreateContext();

        // Act
        var index = context.Model.FindEntityType(typeof(Account))!.GetIndexes().Where(i => i.Properties.Count == 1 && i.Properties[0].Name == nameof(Account.Email)).ToList().ShouldHaveSingleItem();

        // Assert
        index.IsUnique.ShouldBeTrue();
        index.FindAnnotation(UniqueRuleIndexBuilderExtensions.AnnotationName)!.Value.ShouldBe(AccountDbContext.EmailRule);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void A_blank_rule_is_rejected(string rule)
    {
        // Arrange
        var builder = new ModelBuilder().Entity<Account>().HasIndex(account => account.Email);
        Action act = () => builder.HasUniqueRule(rule);

        // Act & Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void A_null_builder_is_rejected()
    {
        // Arrange
        Action act = () => UniqueRuleIndexBuilderExtensions.HasUniqueRule<Account>(null!, "rule");

        // Act & Assert
        act.ShouldThrow<ArgumentNullException>();
    }
}
