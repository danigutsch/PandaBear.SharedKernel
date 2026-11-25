using Microsoft.EntityFrameworkCore;
using SharedKernel.Mediator;
using SharedKernel.Results;

namespace SharedKernel.EntityFrameworkCore.Sqlite.Tests;

/// <summary>Tests that a save reports real SQLite concurrency conflicts and tagged unique violations as results.</summary>
[Trait("Scope", "Integration")]
[Trait("Capability", "ConcurrencyConflicts")]
public sealed class SaveAsResultTests
{
    private static readonly SqliteUniqueViolationParser Parser = new();

    [Fact]
    public async Task Saving_without_conflicts_returns_success_and_writes_the_row()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var account = new Account();
        context.Accounts.Add(account);

        // Act
        var result = await context.SaveChangesAsResult(Parser, SaveResults.UnexpectedRule, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(Result.Ok());
        await using var check = database.CreateContext();
        var stored = await check.Accounts.CountAsync(row => row.Id == account.Id, TestContext.Current.CancellationToken);
        stored.ShouldBe(1);
    }

    [Fact]
    public async Task A_duplicate_on_a_tagged_index_returns_the_result_mapped_from_its_rule_and_clears_the_tracker()
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
        var rules = new List<string>();

        // Act
        var result = await context.SaveChangesAsResult(Parser, rule => SaveResults.Recorded(rules, rule), TestContext.Current.CancellationToken);

        // Assert
        rules.ShouldBe([AccountDbContext.EmailRule]);
        result.ShouldBe(SaveResults.FieldError);
        context.ChangeTracker.Entries().ShouldBeEmpty();
    }

    [Fact]
    public async Task Updating_a_row_deleted_since_it_was_loaded_returns_the_standard_stale_conflict_and_clears_the_tracker()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var id = await ConflictOutcomes.Seed(database);
        await using var context = database.CreateContext();
        var loaded = await context.Accounts.SingleAsync(account => account.Id == id, TestContext.Current.CancellationToken);
        await ConflictOutcomes.DeleteElsewhere(database, id);
        loaded.Name = "changed";

        // Act
        var result = await context.SaveChangesAsResult(Parser, SaveResults.UnexpectedRule, TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(ResultStatus.Conflict);
        result.ErrorDetails!.Code.ShouldBe(ResultErrorCodes.ConcurrencyConflict);
        result.ErrorDetails.Detail.ShouldBe(DbContextResultExtensions.ConcurrencyConflictMessage);
        result.ShouldBe(ConcurrencyConflictResults.Stale());
        context.ChangeTracker.Entries().ShouldBeEmpty();
    }

    [Fact]
    public async Task A_failure_that_is_not_a_conflict_or_tagged_violation_propagates_and_keeps_the_tracker()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await using (var seed = database.CreateContext())
        {
            seed.Accounts.Add(new Account { Email = "first@example.com", Code = "a", Nickname = "same" });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var context = database.CreateContext();
        context.Accounts.Add(new Account { Email = "second@example.com", Code = "b", Nickname = "same" });

        // Act
        var thrown = await SaveResults.FailedSave(context, Parser);

        // Assert
        thrown.ShouldBeOfType<DbUpdateException>();
        context.ChangeTracker.Entries().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Null_arguments_are_rejected()
    {
        // Arrange
        Func<Task> noContext = () => DbContextResultExtensions.SaveChangesAsResult(null!, Parser, SaveResults.UnexpectedRule, TestContext.Current.CancellationToken);
        Func<Task> noParser = () => new AccountDbContext(new DbContextOptions<AccountDbContext>()).SaveChangesAsResult(null!, SaveResults.UnexpectedRule, TestContext.Current.CancellationToken);
        Func<Task> noMapping = () => new AccountDbContext(new DbContextOptions<AccountDbContext>()).SaveChangesAsResult(Parser, null!, TestContext.Current.CancellationToken);

        // Act & Assert
        await noContext.ShouldThrow<ArgumentNullException>();
        await noParser.ShouldThrow<ArgumentNullException>();
        await noMapping.ShouldThrow<ArgumentNullException>();
    }
}
