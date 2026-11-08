using Microsoft.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Sqlite.Tests;

/// <summary>Tests that real SQLite violations are reported by application rule.</summary>
[Trait("Scope", "Integration")]
[Trait("Capability", "UniqueViolations")]
public sealed class SaveTranslationTests
{
    private static readonly SqliteUniqueViolationParser Parser = new();

    [Fact]
    public async Task Saving_without_violations_returns_the_written_count()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        context.Accounts.Add(new Account());

        // Act
        var written = await context.SaveChangesTranslatingUniqueViolations(Parser, TestContext.Current.CancellationToken);

        // Assert
        written.ShouldBe(1);
    }

    [Fact]
    public async Task A_duplicate_on_a_tagged_index_is_reported_by_rule_with_the_provider_exception_kept()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await using (var seed = database.CreateContext())
        {
            seed.Accounts.Add(new Account { Email = "same@example.com", Code = "a" });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var context = database.CreateContext();
        context.Accounts.Add(new Account { Email = "same@example.com", Code = "b" });

        // Act
        var thrown = await SaveOutcomes.ExpectRule(context, Parser);

        // Assert
        thrown.Rule.ShouldBe(AccountDbContext.EmailRule);
        thrown.InnerException.ShouldBeOfType<DbUpdateException>();
    }

    [Fact]
    public async Task A_duplicate_on_a_composite_tagged_index_is_reported_by_its_rule()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await using (var seed = database.CreateContext())
        {
            seed.Accounts.Add(new Account { TenantId = 7, Code = "x", Email = "one@example.com" });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var context = database.CreateContext();
        context.Accounts.Add(new Account { TenantId = 7, Code = "x", Email = "two@example.com" });

        // Act
        var thrown = await SaveOutcomes.ExpectRule(context, Parser);

        // Assert
        thrown.Rule.ShouldBe(AccountDbContext.CodeRule);
    }

    [Fact]
    public async Task The_same_code_for_another_tenant_is_not_a_violation()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        context.Accounts.Add(new Account { TenantId = 1, Code = "x", Email = "one@example.com" });
        context.Accounts.Add(new Account { TenantId = 2, Code = "x", Email = "two@example.com" });

        // Act
        var written = await context.SaveChangesTranslatingUniqueViolations(Parser, TestContext.Current.CancellationToken);

        // Assert
        written.ShouldBe(2);
    }

    [Fact]
    public async Task A_duplicate_on_an_untagged_unique_index_propagates_unchanged()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await using (var seed = database.CreateContext())
        {
            seed.Accounts.Add(new Account { Nickname = "nick", Email = "one@example.com", Code = "a" });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var context = database.CreateContext();
        context.Accounts.Add(new Account { Nickname = "nick", Email = "two@example.com", Code = "b" });

        // Act
        var thrown = await SaveOutcomes.ExpectUnchanged(context, Parser);

        // Assert
        thrown.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_primary_key_violation_propagates_unchanged()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var id = Guid.NewGuid();
        await using (var seed = database.CreateContext())
        {
            seed.Accounts.Add(new Account { Id = id, Email = "one@example.com", Code = "a" });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var context = database.CreateContext();
        context.Accounts.Add(new Account { Id = id, Email = "two@example.com", Code = "b" });

        // Act
        var thrown = await SaveOutcomes.ExpectUnchanged(context, Parser);

        // Assert
        thrown.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_failure_that_is_not_a_unique_violation_propagates_unchanged()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        context.Accounts.Add(new Account { Name = null });

        // Act
        var thrown = await SaveOutcomes.ExpectUnchanged(context, Parser);

        // Assert
        thrown.ShouldNotBeNull();
    }

    [Fact]
    public async Task Null_arguments_are_rejected()
    {
        // Arrange
        Func<Task> noContext = () => DbContextUniqueViolationExtensions.SaveChangesTranslatingUniqueViolations(null!, Parser, TestContext.Current.CancellationToken);
        Func<Task> noParser = () => new AccountDbContext(new DbContextOptions<AccountDbContext>()).SaveChangesTranslatingUniqueViolations(null!, TestContext.Current.CancellationToken);

        // Act & Assert
        await noContext.ShouldThrow<ArgumentNullException>();
        await noParser.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public async Task A_provider_report_matching_no_tagged_index_propagates_unchanged()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await using (var seed = database.CreateContext())
        {
            seed.Accounts.Add(new Account { Email = "same@example.com", Code = "a" });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var context = database.CreateContext();
        context.Accounts.Add(new Account { Email = "same@example.com", Code = "b" });
        var unrelated = new FixedUniqueViolationParser(new UniqueViolation(null, "Other", ["Email"]));

        // Act
        var thrown = await SaveOutcomes.ExpectUnchanged(context, unrelated);

        // Assert
        thrown.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("accounts", "email")]
    [InlineData("ACCOUNTS", "EMAIL")]
    public async Task Table_and_column_matching_ignores_case(string table, string column)
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await using (var seed = database.CreateContext())
        {
            seed.Accounts.Add(new Account { Email = "same@example.com", Code = "a" });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var context = database.CreateContext();
        context.Accounts.Add(new Account { Email = "same@example.com", Code = "b" });
        var parser = new FixedUniqueViolationParser(new UniqueViolation(null, table, [column]));

        // Act
        var thrown = await SaveOutcomes.ExpectRule(context, parser);

        // Assert
        thrown.Rule.ShouldBe(AccountDbContext.EmailRule);
    }

    [Fact]
    public async Task A_constraint_name_matches_the_index_database_name()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await using (var seed = database.CreateContext())
        {
            seed.Accounts.Add(new Account { Email = "same@example.com", Code = "a" });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var context = database.CreateContext();
        context.Accounts.Add(new Account { Email = "same@example.com", Code = "b" });
        var parser = new FixedUniqueViolationParser(new UniqueViolation("IX_Accounts_Email", null, []));

        // Act
        var thrown = await SaveOutcomes.ExpectRule(context, parser);

        // Assert
        thrown.Rule.ShouldBe(AccountDbContext.EmailRule);
    }

    [Fact]
    public async Task Partial_column_reports_do_not_match_a_composite_index()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await using (var seed = database.CreateContext())
        {
            seed.Accounts.Add(new Account { TenantId = 3, Code = "x", Email = "one@example.com" });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var context = database.CreateContext();
        context.Accounts.Add(new Account { TenantId = 3, Code = "x", Email = "two@example.com" });
        var partial = new FixedUniqueViolationParser(new UniqueViolation(null, "Accounts", ["Code"]));

        // Act
        var thrown = await SaveOutcomes.ExpectUnchanged(context, partial);

        // Assert
        thrown.ShouldNotBeNull();
    }
}
