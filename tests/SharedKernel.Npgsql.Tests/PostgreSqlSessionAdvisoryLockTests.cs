namespace SharedKernel.Npgsql.Tests;

public sealed class PostgreSqlSessionAdvisoryLockTests(PostgreSqlTestServerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture)
{
    private const long LockKey = 893_492_733;

    [Fact]
    public async Task Holds_the_lock_until_the_session_lease_is_disposed()
    {
        // Arrange
        var lease = await PostgreSqlSessionAdvisoryLock.Acquire(
            DataSource,
            LockKey,
            TestContext.Current.CancellationToken);

        // Act
        var acquiredWhileHeld = await PostgreSqlTransactionAdvisoryLockTestsHelpers.TryAcquire(
            DataSource,
            LockKey,
            TestContext.Current.CancellationToken);
        await lease.DisposeAsync();
        var acquiredAfterDisposal = await PostgreSqlTransactionAdvisoryLockTestsHelpers.TryAcquire(
            DataSource,
            LockKey,
            TestContext.Current.CancellationToken);

        // Assert
        acquiredWhileHeld.ShouldBeFalse();
        acquiredAfterDisposal.ShouldBeTrue();
    }
}
