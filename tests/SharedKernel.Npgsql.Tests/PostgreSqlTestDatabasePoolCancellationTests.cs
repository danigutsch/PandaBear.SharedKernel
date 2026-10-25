namespace PandaBear.Npgsql.Tests;

public sealed class PostgreSqlTestDatabasePoolCancellationTests(PostgreSqlTestServerFixture fixture)
{
    // The existing pool test cancels before the pool takes its gate, so no name is registered. Cancelling inside the
    // resolver lets the creation continue past the gate: the pool then registers a new name and creates the database
    // with a cancelled token.
    [Fact]
    public async Task Creation_cancelled_after_the_server_is_resolved_releases_the_registered_name()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var bootstrap = await fixture.CreateDatabase(ct);
        using var cancellation = new CancellationTokenSource();
        await using var pool = new PostgreSqlTestDatabasePool(async _ =>
        {
            await cancellation.CancelAsync();
            return bootstrap.ConnectionString;
        });
        Func<Task> cancelledCreate = () => pool.Create(cancellation.Token);

        // Act
        _ = await cancelledCreate.ShouldThrowAssignableTo<OperationCanceledException>();
        var namesAfterCancellation = pool.OwnedDatabaseNames;
        await using var lease = await pool.Create(ct);
        var exists = await fixture.DatabaseExists(lease.DatabaseName, ct);

        // Assert
        namesAfterCancellation.ShouldBeEmpty("the cleanup after a cancelled creation must release the registered name");
        exists.ShouldBeTrue("the pool keeps issuing databases after a cancelled creation");
        pool.OwnedDatabaseNames.ShouldHaveSingleItem().ShouldBe(lease.DatabaseName);
    }
}
