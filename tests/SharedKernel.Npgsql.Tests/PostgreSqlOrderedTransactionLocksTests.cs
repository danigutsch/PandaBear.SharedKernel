namespace PandaBear.Npgsql.Tests;

/// <summary>Tests the ordered transaction-scoped PostgreSQL advisory locks.</summary>
/// <param name="fixture">The shared PostgreSQL test server fixture that supplies the per-test database.</param>
public sealed class PostgreSqlOrderedTransactionLocksTests(PostgreSqlTestServerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture)
{
    [Fact]
    public async Task Takes_every_requested_lock_and_releases_them_on_commit()
    {
        // Arrange
        var names = PostgreSqlOrderedTransactionLocksTestsHelpers.NamesByKey("held-a", "held-b");
        await using var connection = await DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var locks = new PostgreSqlOrderedTransactionLocks(connection, transaction);

        // Act
        await locks.Acquire([new(0, names[0]), new(1, names[1])], TestContext.Current.CancellationToken);
        var freeWhileHeld = await PostgreSqlOrderedTransactionLocksTestsHelpers.FreeNames(DataSource, names, TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
        var freeAfterCommit = await PostgreSqlOrderedTransactionLocksTestsHelpers.FreeNames(DataSource, names, TestContext.Current.CancellationToken);

        // Assert
        freeWhileHeld.ShouldBeEmpty();
        string.Join(",", freeAfterCommit.Order(StringComparer.Ordinal)).ShouldBe(string.Join(",", names.Order(StringComparer.Ordinal)));
    }

    [Fact]
    public async Task Takes_locks_in_tier_then_key_order_regardless_of_request_order()
    {
        // Arrange
        var sameTier = PostgreSqlOrderedTransactionLocksTestsHelpers.NamesByKey("order-a", "order-b");
        const string higherTierName = "order-tier-1";
        await using var blocker = await PostgreSqlOrderedTransactionLocksTestsHelpers.Hold(DataSource, sameTier[1], TestContext.Current.CancellationToken);
        await using var connection = await DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var locks = new PostgreSqlOrderedTransactionLocks(connection, transaction);

        // Act
        var acquisition = locks.Acquire(
            [new(1, higherTierName), new(0, sameTier[1]), new(0, sameTier[0])],
            TestContext.Current.CancellationToken).AsTask();
        await PostgreSqlTransactionAdvisoryLockTestsHelpers.WaitForWaitingLock(DataSource, connection.ProcessID, TestContext.Current.CancellationToken);
        var freeWhileWaiting = await PostgreSqlOrderedTransactionLocksTestsHelpers.FreeNames(
            DataSource,
            [sameTier[0], higherTierName],
            TestContext.Current.CancellationToken);
        await blocker.Release(TestContext.Current.CancellationToken);
        await acquisition;

        // Assert
        freeWhileWaiting.ShouldBe([higherTierName]);
    }

    [Fact]
    public async Task Accepts_a_request_for_a_lock_the_transaction_already_holds()
    {
        // Arrange
        await using var connection = await DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var locks = new PostgreSqlOrderedTransactionLocks(connection, transaction);
        await locks.Acquire([new(1, "repeat-high")], TestContext.Current.CancellationToken);

        // Act
        await locks.Acquire([new(0, "repeat-high"), new(1, "repeat-high"), new(1, "repeat-high")], TestContext.Current.CancellationToken);

        // Assert
        transaction.Connection.ShouldNotBeNull();
    }

    [Fact]
    public async Task Rejects_a_lower_tier_after_a_higher_tier_and_names_the_lock()
    {
        // Arrange
        await using var connection = await DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var locks = new PostgreSqlOrderedTransactionLocks(connection, transaction);
        await locks.Acquire([new(1, "hold-guard")], TestContext.Current.CancellationToken);

        // Act
        Func<Task> acquireLowerTier = () => locks.Acquire([new(0, "tour-capacity")], TestContext.Current.CancellationToken).AsTask();
        var exception = await acquireLowerTier.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("tour-capacity", StringComparison.Ordinal);
        exception.Message.ShouldContain("hold-guard", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejects_a_smaller_key_after_a_larger_key_within_one_tier()
    {
        // Arrange
        var names = PostgreSqlOrderedTransactionLocksTestsHelpers.NamesByKey("tier-a", "tier-b");
        await using var connection = await DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var locks = new PostgreSqlOrderedTransactionLocks(connection, transaction);
        await locks.Acquire([new(1, names[1])], TestContext.Current.CancellationToken);

        // Act
        Func<Task> acquireSmallerKey = () => locks.Acquire([new(1, names[0])], TestContext.Current.CancellationToken).AsTask();
        var exception = await acquireSmallerKey.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain(names[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Allows_a_larger_key_in_the_same_tier_in_a_later_call()
    {
        // Arrange
        var names = PostgreSqlOrderedTransactionLocksTestsHelpers.NamesByKey("later-a", "later-b");
        await using var connection = await DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var locks = new PostgreSqlOrderedTransactionLocks(connection, transaction);
        await locks.Acquire([new(1, names[0])], TestContext.Current.CancellationToken);

        // Act
        await locks.Acquire([new(1, names[1])], TestContext.Current.CancellationToken);
        var freeWhileHeld = await PostgreSqlOrderedTransactionLocksTestsHelpers.FreeNames(DataSource, names, TestContext.Current.CancellationToken);

        // Assert
        freeWhileHeld.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_rejected_request_takes_none_of_its_locks()
    {
        // Arrange
        await using var connection = await DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var locks = new PostgreSqlOrderedTransactionLocks(connection, transaction);
        await locks.Acquire([new(1, "reject-held")], TestContext.Current.CancellationToken);

        // Act
        Func<Task> acquireMixed = () => locks.Acquire([new(2, "reject-fine"), new(0, "reject-bad")], TestContext.Current.CancellationToken).AsTask();
        _ = await acquireMixed.ShouldThrow<InvalidOperationException>();
        var free = await PostgreSqlOrderedTransactionLocksTestsHelpers.FreeNames(DataSource, ["reject-fine", "reject-bad"], TestContext.Current.CancellationToken);

        // Assert
        string.Join(",", free.Order(StringComparer.Ordinal)).ShouldBe("reject-bad,reject-fine");
    }

    [Fact]
    public async Task A_new_instance_for_the_next_transaction_on_the_connection_starts_with_a_clean_order_state()
    {
        // Arrange
        await using var connection = await DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using (var first = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            await new PostgreSqlOrderedTransactionLocks(connection, first).Acquire([new(1, "retry-high")], TestContext.Current.CancellationToken);
            await first.RollbackAsync(TestContext.Current.CancellationToken);
        }

        await using var second = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        // Act
        await new PostgreSqlOrderedTransactionLocks(connection, second).Acquire([new(0, "retry-low")], TestContext.Current.CancellationToken);
        var free = await PostgreSqlOrderedTransactionLocksTestsHelpers.FreeNames(DataSource, ["retry-low"], TestContext.Current.CancellationToken);

        // Assert
        free.ShouldBeEmpty();
    }

    [Fact]
    public async Task Takes_a_lock_again_after_a_rollback_to_a_savepoint_released_it()
    {
        // Arrange
        await using var connection = await DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var locks = new PostgreSqlOrderedTransactionLocks(connection, transaction);
        await transaction.SaveAsync("before-lock", TestContext.Current.CancellationToken);
        await locks.Acquire([new(0, "savepoint-lock")], TestContext.Current.CancellationToken);
        await transaction.RollbackAsync("before-lock", TestContext.Current.CancellationToken);

        // Act
        await locks.Acquire([new(0, "savepoint-lock")], TestContext.Current.CancellationToken);
        var free = await PostgreSqlOrderedTransactionLocksTestsHelpers.FreeNames(DataSource, ["savepoint-lock"], TestContext.Current.CancellationToken);

        // Assert
        free.ShouldBeEmpty();
    }

    [Fact]
    public async Task Refuses_to_acquire_once_the_transaction_is_complete()
    {
        // Arrange
        await using var connection = await DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var locks = new PostgreSqlOrderedTransactionLocks(connection, transaction);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        // Act
        Func<Task> acquire = () => locks.Acquire([new(0, "closed")], TestContext.Current.CancellationToken).AsTask();
        _ = await acquire.ShouldThrow<InvalidOperationException>();

        // Assert
        var free = await PostgreSqlOrderedTransactionLocksTestsHelpers.FreeNames(DataSource, ["closed"], TestContext.Current.CancellationToken);
        free.ShouldBe(["closed"]);
    }

    [Fact]
    public async Task Rejects_a_transaction_that_belongs_to_another_connection()
    {
        // Arrange
        await using var connection = await DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var otherConnection = await DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await otherConnection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        // Act
        Action create = () => _ = new PostgreSqlOrderedTransactionLocks(connection, transaction);

        // Assert
        _ = create.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public async Task Rejects_invalid_requests_before_taking_any_lock()
    {
        // Arrange
        await using var connection = await DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var locks = new PostgreSqlOrderedTransactionLocks(connection, transaction);

        // Act
        Func<Task> negativeTier = () => locks.Acquire([new(0, "valid"), new(-1, "negative")], TestContext.Current.CancellationToken).AsTask();
        Func<Task> missingName = () => locks.Acquire([new(0, "valid"), new(0, null!)], TestContext.Current.CancellationToken).AsTask();
        _ = await negativeTier.ShouldThrow<ArgumentOutOfRangeException>();
        _ = await missingName.ShouldThrowAssignableTo<ArgumentException>();
        var free = await PostgreSqlOrderedTransactionLocksTestsHelpers.FreeNames(DataSource, ["valid"], TestContext.Current.CancellationToken);

        // Assert
        free.ShouldBe(["valid"]);
    }
}
