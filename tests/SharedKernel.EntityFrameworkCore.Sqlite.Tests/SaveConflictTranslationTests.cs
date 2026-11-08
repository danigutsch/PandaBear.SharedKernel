using Microsoft.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Sqlite.Tests;

/// <summary>Tests that real SQLite concurrency conflicts and unique violations are translated.</summary>
[Trait("Scope", "Integration")]
[Trait("Capability", "ConcurrencyConflicts")]
public sealed class SaveConflictTranslationTests
{
    private static readonly SqliteUniqueViolationParser Parser = new();

    [Fact]
    public async Task Saving_without_conflicts_returns_the_written_count()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        context.Accounts.Add(new Account());

        // Act
        var written = await context.SaveChangesTranslatingConflicts(Parser, TestContext.Current.CancellationToken);

        // Assert
        written.ShouldBe(1);
    }

    [Fact]
    public async Task Updating_a_row_deleted_since_it_was_loaded_is_reported_as_a_conflict_with_the_cause_kept()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var id = await ConflictOutcomes.Seed(database);
        await using var context = database.CreateContext();
        var loaded = await context.Accounts.SingleAsync(account => account.Id == id, TestContext.Current.CancellationToken);
        await ConflictOutcomes.DeleteElsewhere(database, id);
        loaded.Name = "changed";

        // Act
        var thrown = await ConflictOutcomes.ExpectConflict(context, Parser);

        // Assert
        thrown.InnerException.ShouldBeOfType<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task Deleting_a_row_deleted_since_it_was_loaded_is_reported_as_a_conflict()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var id = await ConflictOutcomes.Seed(database);
        await using var context = database.CreateContext();
        var loaded = await context.Accounts.SingleAsync(account => account.Id == id, TestContext.Current.CancellationToken);
        await ConflictOutcomes.DeleteElsewhere(database, id);
        context.Accounts.Remove(loaded);

        // Act
        var thrown = await ConflictOutcomes.ExpectConflict(context, Parser);

        // Assert
        thrown.InnerException.ShouldBeOfType<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task A_duplicate_on_a_tagged_index_is_still_reported_by_rule()
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
        var thrown = await SaveOutcomes.ExpectRule(context, Parser, translateConflicts: true);

        // Assert
        thrown.Rule.ShouldBe(AccountDbContext.EmailRule);
    }

    [Fact]
    public async Task A_failure_that_is_not_a_conflict_or_unique_violation_propagates_unchanged()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        context.Accounts.Add(new Account { Name = null });

        // Act
        var thrown = await SaveOutcomes.ExpectUnchanged(context, Parser, translateConflicts: true);

        // Assert
        thrown.ShouldNotBeNull();
    }

    [Fact]
    public async Task Null_arguments_are_rejected()
    {
        // Arrange
        Func<Task> noContext = () => DbContextUniqueViolationExtensions.SaveChangesTranslatingConflicts(null!, Parser, TestContext.Current.CancellationToken);
        Func<Task> noParser = () => new AccountDbContext(new DbContextOptions<AccountDbContext>()).SaveChangesTranslatingConflicts(null!, TestContext.Current.CancellationToken);

        // Act & Assert
        await noContext.ShouldThrow<ArgumentNullException>();
        await noParser.ShouldThrow<ArgumentNullException>();
    }
}
