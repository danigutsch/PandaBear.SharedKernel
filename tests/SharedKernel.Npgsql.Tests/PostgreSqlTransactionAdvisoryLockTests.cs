namespace SharedKernel.Npgsql.Tests;

public sealed class PostgreSqlTransactionAdvisoryLockTests(PostgreSqlTestServerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture)
{
    private const long LockKey = 893_492_731;
    private const long DifferentLockKey = 893_492_732;

    [Fact]
    public async Task Waiting_for_an_advisory_lock_propagates_cancellation()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        Func<Task> waitForLock = () => PostgreSqlTransactionAdvisoryLockTestsHelpers.WaitForWaitingLock(
            DataSource,
            processId: 0,
            cancellation.Token);
        var exception = await waitForLock.ShouldThrowAssignableTo<OperationCanceledException>();

        // Assert
        exception.ShouldNotBeNull();
    }

    [Fact]
    public async Task Prevents_another_connection_from_acquiring_the_same_lock_until_commit()
    {
        // Arrange
        await using var connection = await DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        // Act
        await PostgreSqlTransactionAdvisoryLock.Acquire(connection, transaction, LockKey, TestContext.Current.CancellationToken);
        var acquiredWhileHeld = await PostgreSqlTransactionAdvisoryLockTestsHelpers.TryAcquire(
            DataSource,
            LockKey,
            TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
        var acquiredAfterCommit = await PostgreSqlTransactionAdvisoryLockTestsHelpers.TryAcquire(
            DataSource,
            LockKey,
            TestContext.Current.CancellationToken);

        // Assert
        acquiredWhileHeld.ShouldBeFalse();
        acquiredAfterCommit.ShouldBeTrue();
    }

    [Fact]
    public async Task Releases_the_lock_when_the_transaction_rolls_back()
    {
        // Arrange
        await using var connection = await DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        // Act
        await PostgreSqlTransactionAdvisoryLock.Acquire(connection, transaction, LockKey, TestContext.Current.CancellationToken);
        await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        var acquiredAfterRollback = await PostgreSqlTransactionAdvisoryLockTestsHelpers.TryAcquire(
            DataSource,
            LockKey,
            TestContext.Current.CancellationToken);

        // Assert
        acquiredAfterRollback.ShouldBeTrue();
    }

    [Fact]
    public async Task Releases_the_lock_when_the_transaction_is_disposed()
    {
        // Arrange
        await using var connection = await DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);

        // Act
        await using (var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            await PostgreSqlTransactionAdvisoryLock.Acquire(connection, transaction, LockKey, TestContext.Current.CancellationToken);
        }

        var acquiredAfterDisposal = await PostgreSqlTransactionAdvisoryLockTestsHelpers.TryAcquire(
            DataSource,
            LockKey,
            TestContext.Current.CancellationToken);

        // Assert
        acquiredAfterDisposal.ShouldBeTrue();
    }

    [Fact]
    public async Task Allows_different_lock_keys_to_be_held_concurrently()
    {
        // Arrange
        await using var connection = await DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await PostgreSqlTransactionAdvisoryLock.Acquire(connection, transaction, LockKey, TestContext.Current.CancellationToken);

        // Act
        var acquiredDifferentKey = await PostgreSqlTransactionAdvisoryLockTestsHelpers.TryAcquire(
            DataSource,
            DifferentLockKey,
            TestContext.Current.CancellationToken);

        // Assert
        acquiredDifferentKey.ShouldBeTrue();
    }

    [Fact]
    public async Task Cancels_a_waiting_lock_acquisition_without_releasing_the_holder_lock()
    {
        // Arrange
        await using var holderConnection = await DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var holderTransaction = await holderConnection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await PostgreSqlTransactionAdvisoryLock.Acquire(
            holderConnection,
            holderTransaction,
            LockKey,
            TestContext.Current.CancellationToken);
        await using var waitingConnection = await DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var waitingTransaction = await waitingConnection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();

        // Act
        var waitingAcquisition = PostgreSqlTransactionAdvisoryLock.Acquire(
            waitingConnection,
            waitingTransaction,
            LockKey,
            cancellation.Token).AsTask();
        await PostgreSqlTransactionAdvisoryLockTestsHelpers.WaitForWaitingLock(
            DataSource,
            waitingConnection.ProcessID,
            TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        Func<Task> awaitWaitingAcquisition = () => waitingAcquisition;
        var exception = await awaitWaitingAcquisition.ShouldThrowAssignableTo<OperationCanceledException>();
        var acquiredWhileHolderRemains = await PostgreSqlTransactionAdvisoryLockTestsHelpers.TryAcquire(
            DataSource,
            LockKey,
            TestContext.Current.CancellationToken);
        await holderTransaction.RollbackAsync(TestContext.Current.CancellationToken);
        var acquiredAfterHolderRollback = await PostgreSqlTransactionAdvisoryLockTestsHelpers.TryAcquire(
            DataSource,
            LockKey,
            TestContext.Current.CancellationToken);

        // Assert
        exception.ShouldNotBeNull();
        acquiredWhileHolderRemains.ShouldBeFalse();
        acquiredAfterHolderRollback.ShouldBeTrue();
    }
}
