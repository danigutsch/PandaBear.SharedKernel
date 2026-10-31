using System.Data;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using PandaBear.Results;
using H = PandaBear.EntityFrameworkCore.Tests.ResultTransactionBehaviorTestsHelpers;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// PostgreSQL tests of ResultTransactionBehavior: commit and rollback by result, declared locks, retries, hooks and isolation.
/// </summary>
/// <param name="fixture">Shared PostgreSQL fixture that supplies the isolated database.</param>
[Trait(SharedKernelTestTraitNames.CategoryName, TestTraits.DatabaseIntegrationCategory)]
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.ResultTransactionBehaviorCapability)]
[SuppressMessage(
    "Usage",
    "CA2213:Disposable fields should be disposed",
    Justification = "DisposeAsync passes the data source to the independent aggregate cleanup helper.")]
public sealed class ResultTransactionBehaviorTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private static readonly ResultTransactionTestCommand NoLocks = new([]);

    private PostgreSqlTestDatabaseLease? database;
    private NpgsqlDataSource? dataSource;

    private NpgsqlDataSource DataSource =>
        dataSource ?? throw new InvalidOperationException("PostgreSQL test database is not initialized.");

    /// <summary>Creates an isolated PostgreSQL database and its schema.</summary>
    public async ValueTask InitializeAsync()
    {
        database = await fixture.CreateIsolatedDatabase(TestContext.Current.CancellationToken);
        dataSource = database.CreateDataSource(nameof(ResultTransactionBehaviorTests));
        var options = new DbContextOptionsBuilder<TestDbContext>().UseNpgsql(dataSource).Options;
        await using var context = new TestDbContext(options);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Disposes the data source and releases the isolated database.</summary>
    public async ValueTask DisposeAsync()
    {
        var currentDataSource = dataSource;
        var currentDatabase = database;
        dataSource = null;
        database = null;

        await PostgreSqlScenarioCleanup.DisposeResources(null, currentDataSource, currentDatabase);
    }

    [Fact]
    public async Task Commits_a_successful_result_and_runs_the_after_commit_hook_once()
    {
        // Arrange
        await using var scenario = new ResultTransactionTestScenario(DataSource);
        var (behavior, context, scope) = scenario.CreateBehavior();
        using var scopeLease = scope;

        // Act
        var result = await behavior.Handle(NoLocks, () => H.AddAndSave(context, "committed", Result.Ok()), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var persisted = await H.CountNamed(DataSource, "committed");
        persisted.ShouldBe(1);
        scenario.Hooks.Calls.ShouldBe(["commit"]);
    }

    [Fact]
    public async Task Rolls_back_a_failed_result_and_runs_the_after_rollback_hook_once()
    {
        // Arrange
        await using var scenario = new ResultTransactionTestScenario(DataSource);
        var (behavior, context, scope) = scenario.CreateBehavior();
        using var scopeLease = scope;

        // Act
        var result = await behavior.Handle(
            NoLocks,
            () => H.AddAndSave(context, "rejected", Result.NotFound("missing")),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        var persisted = await H.CountNamed(DataSource, "rejected");
        persisted.ShouldBe(0);
        context.ChangeTracker.Entries().ShouldBeEmpty();
        scenario.Hooks.Calls.ShouldBe(["rollback"]);
    }

    [Fact]
    public async Task Rolls_back_a_default_result_because_it_is_not_successful()
    {
        // Arrange
        await using var scenario = new ResultTransactionTestScenario(DataSource);
        var (behavior, context, scope) = scenario.CreateBehavior();
        using var scopeLease = scope;

        // Act
        var result = await behavior.Handle(NoLocks, () => H.AddAndSave(context, "default", default), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        var persisted = await H.CountNamed(DataSource, "default");
        persisted.ShouldBe(0);
    }

    [Fact]
    public async Task Rolls_back_and_propagates_an_exception_without_running_hooks()
    {
        // Arrange
        await using var scenario = new ResultTransactionTestScenario(DataSource);
        var (behavior, context, scope) = scenario.CreateBehavior();
        using var scopeLease = scope;

        // Act
        var act = async () => await behavior.Handle(
            NoLocks,
            async () =>
            {
                _ = await H.AddAndSave(context, "thrown", Result.Ok());
                throw new InvalidOperationException("boom");
            },
            TestContext.Current.CancellationToken);

        // Assert
        await act.ShouldThrow<InvalidOperationException>();
        var persisted = await H.CountNamed(DataSource, "thrown");
        persisted.ShouldBe(0);
        scenario.Hooks.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Joins_an_active_transaction_without_committing_it()
    {
        // Arrange
        await using var scenario = new ResultTransactionTestScenario(DataSource);
        var (behavior, context, scope) = scenario.CreateBehavior();
        using var scopeLease = scope;

        // Act
        var result = await context.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
        {
            var outer = await context.Database.BeginTransactionAsync(ct);
            await using var outerLease = outer.ConfigureAwait(false);
            var joined = await behavior.Handle(NoLocks, () => H.AddAndSave(context, "joined", Result.Ok()), ct);
            await outer.RollbackAsync(ct);
            return joined;
        }, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var persisted = await H.CountNamed(DataSource, "joined");
        persisted.ShouldBe(0);
        scenario.Hooks.Calls.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(IsolationLevel.RepeatableRead)]
    [InlineData(IsolationLevel.Serializable)]
    public async Task Refuses_to_join_a_transaction_stricter_than_read_committed(IsolationLevel isolation)
    {
        // Arrange
        await using var scenario = new ResultTransactionTestScenario(DataSource);
        var (behavior, context, scope) = scenario.CreateBehavior();
        using var scopeLease = scope;
        var outer = await context.Database.BeginTransactionAsync(isolation, TestContext.Current.CancellationToken);
        await using var outerLease = outer.ConfigureAwait(false);
        var handlerRan = false;

        // Act
        var act = async () => await behavior.Handle(
            NoLocks,
            () =>
            {
                handlerRan = true;
                return ValueTask.FromResult(Result.Ok());
            },
            TestContext.Current.CancellationToken);

        // Assert
        await act.ShouldThrow<InvalidOperationException>();
        handlerRan.ShouldBeFalse();
    }

    [Fact]
    public async Task Holds_declared_locks_while_the_handler_runs_and_releases_them_on_rollback()
    {
        // Arrange
        const string lockName = "result-transaction:rollback-release";
        await using var scenario = new ResultTransactionTestScenario(DataSource);
        var (behavior, context, scope) = scenario.CreateBehavior();
        using var scopeLease = scope;
        bool? lockFreeWhileRunning = null;

        // Act
        var result = await behavior.Handle(
            new ResultTransactionTestCommand([lockName]),
            async () =>
            {
                lockFreeWhileRunning = await H.TryTakeLock(DataSource, lockName);
                return Result.Conflict("rejected");
            },
            TestContext.Current.CancellationToken);
        var lockFreeAfterRollback = await H.TryTakeLock(DataSource, lockName);

        // Assert
        result.IsFailure.ShouldBeTrue();
        context.ChangeTracker.Entries().ShouldBeEmpty();
        lockFreeWhileRunning.ShouldBe(false);
        lockFreeAfterRollback.ShouldBeTrue();
    }

    [Fact]
    public async Task Collapses_duplicate_declared_locks_so_the_command_does_not_wait_on_itself()
    {
        // Arrange
        const string lockName = "result-transaction:duplicate";
        await using var scenario = new ResultTransactionTestScenario(DataSource);
        var (behavior, _, scope) = scenario.CreateBehavior();
        using var scopeLease = scope;
        bool? lockFreeWhileRunning = null;

        // Act
        var result = await behavior.Handle(
            new ResultTransactionTestCommand([lockName, lockName]),
            async () =>
            {
                lockFreeWhileRunning = await H.TryTakeLock(DataSource, lockName);
                return Result.Ok();
            },
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        lockFreeWhileRunning.ShouldBe(false);
    }

    [Fact]
    public async Task Serializes_parallel_commands_that_declare_the_same_lock()
    {
        // Arrange
        const string lockName = "result-transaction:parallel";
        await using var scenario = new ResultTransactionTestScenario(DataSource);
        var command = new ResultTransactionTestCommand([lockName]);

        // Act
        var maxRunning = await H.MeasureMaxConcurrency(scenario, command, 3);

        // Assert
        maxRunning.ShouldBe(1);
    }

    [Fact]
    public async Task Reruns_the_handler_once_after_a_serialization_failure_and_commits_once()
    {
        // Arrange
        await using var scenario = new ResultTransactionTestScenario(DataSource);
        var (behavior, context, scope) = scenario.CreateBehavior();
        using var scopeLease = scope;
        const string lockName = "result-transaction:retry";
        var attempts = 0;
        var lockHeldPerAttempt = new List<bool>();

        // Act
        var result = await behavior.Handle(
            new ResultTransactionTestCommand([lockName]),
            async () =>
            {
                attempts++;
                lockHeldPerAttempt.Add(!await H.TryTakeLock(DataSource, lockName));
                _ = await H.AddAndSave(context, "retried", Result.Ok());
                return attempts == 1
                    ? throw new PostgresException("could not serialize access", "ERROR", "ERROR", PostgresErrorCodes.SerializationFailure)
                    : Result.Ok();
            },
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        attempts.ShouldBe(2);
        lockHeldPerAttempt.ShouldBe([true, true]);
        var persisted = await H.CountNamed(DataSource, "retried");
        persisted.ShouldBe(1);
        scenario.Hooks.Calls.ShouldBe(["commit"]);
    }

    [Fact]
    public async Task Keeps_the_committed_response_when_the_after_commit_hook_fails_and_logs_it()
    {
        // Arrange
        await using var scenario = new ResultTransactionTestScenario(DataSource);
        scenario.Hooks.Throw = true;
        var (behavior, context, scope) = scenario.CreateBehavior();
        using var scopeLease = scope;

        // Act
        var result = await behavior.Handle(NoLocks, () => H.AddAndSave(context, "hook-commit", Result.Ok()), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var persisted = await H.CountNamed(DataSource, "hook-commit");
        persisted.ShouldBe(1);
        scenario.Logger.Records.Count(record => record.Level == LogLevel.Error).ShouldBe(1);
    }

    [Fact]
    public async Task Keeps_the_failed_result_when_the_after_rollback_hook_fails_and_logs_it()
    {
        // Arrange
        await using var scenario = new ResultTransactionTestScenario(DataSource);
        scenario.Hooks.Throw = true;
        var (behavior, context, scope) = scenario.CreateBehavior();
        using var scopeLease = scope;

        // Act
        var result = await behavior.Handle(
            NoLocks,
            () => H.AddAndSave(context, "hook-rollback", Result.NotFound("missing")),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        scenario.Logger.Records.Count(record => record.Level == LogLevel.Error).ShouldBe(1);
    }
}
