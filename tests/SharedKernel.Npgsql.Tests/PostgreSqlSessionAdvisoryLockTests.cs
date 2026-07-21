namespace SharedKernel.Npgsql.Tests;

public sealed class PostgreSqlSessionAdvisoryLockTests
{
    private const long LockKey = 893_492_733;

    [Fact]
    public async Task Holds_the_lock_until_the_session_lease_is_disposed()
    {
        // Arrange
        await using var environment = await PostgreSqlTransactionAdvisoryLockTestEnvironment.Start(TestContext.Current.CancellationToken);
        var lease = await PostgreSqlSessionAdvisoryLock.Acquire(
            environment.DataSource,
            LockKey,
            TestContext.Current.CancellationToken);

        // Act
        var acquiredWhileHeld = await PostgreSqlTransactionAdvisoryLockTestsHelpers.TryAcquire(
            environment.DataSource,
            LockKey,
            TestContext.Current.CancellationToken);
        await lease.DisposeAsync();
        var acquiredAfterDisposal = await PostgreSqlTransactionAdvisoryLockTestsHelpers.TryAcquire(
            environment.DataSource,
            LockKey,
            TestContext.Current.CancellationToken);

        // Assert
        acquiredWhileHeld.ShouldBeFalse();
        acquiredAfterDisposal.ShouldBeTrue();
    }
}
