namespace PandaBear.Npgsql.Tests;

/// <summary>Tests the test database pool created from a fixed server connection string.</summary>
/// <param name="fixture">The shared PostgreSQL test server fixture that leases the bootstrap and test databases.</param>
public sealed class PostgreSqlTestDatabasePoolTests(PostgreSqlTestServerFixture fixture)
{
    [Fact]
    public async Task Concurrent_leases_use_one_server_with_isolated_databases()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var bootstrap = await fixture.CreateDatabase(ct);
        await using var pool = new PostgreSqlTestDatabasePool(bootstrap.ConnectionString);

        // Act
        var leases = await Task.WhenAll(pool.Create(ct), pool.Create(ct));
        await using var first = leases[0];
        await using var second = leases[1];
        var firstServer = await PostgreSqlTestServerFixtureTestQueries.GetServerIdentity(first.DataSource, ct);
        var secondServer = await PostgreSqlTestServerFixtureTestQueries.GetServerIdentity(second.DataSource, ct);
        await PostgreSqlTestServerFixtureTestQueries.CreateOwnedTable(first.DataSource, "first", ct);
        await PostgreSqlTestServerFixtureTestQueries.CreateOwnedTable(second.DataSource, "second", ct);
        var firstValue = await PostgreSqlTestServerFixtureTestQueries.ReadOwnedValue(first.DataSource, ct);
        var secondValue = await PostgreSqlTestServerFixtureTestQueries.ReadOwnedValue(second.DataSource, ct);

        // Assert
        firstServer.ShouldBe(secondServer);
        first.DatabaseName.ShouldNotBe(second.DatabaseName);
        firstValue.ShouldBe("first");
        secondValue.ShouldBe("second");
    }

    [Fact]
    public async Task Disposal_reclaims_outstanding_leases_without_dropping_a_sibling_pool_database()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var bootstrap = await fixture.CreateDatabase(ct);
        await using var pool = new PostgreSqlTestDatabasePool(bootstrap.ConnectionString);
        await using var siblingPool = new PostgreSqlTestDatabasePool(bootstrap.ConnectionString);
        await using var lease = await pool.Create(ct);
        await using var sibling = await siblingPool.Create(ct);
        var databaseName = lease.DatabaseName;

        // Act
        await pool.DisposeAsync();
        var exists = await fixture.DatabaseExists(databaseName, ct);
        await PostgreSqlTestServerFixtureTestQueries.CreateOwnedTable(sibling.DataSource, "survives", ct);
        var value = await PostgreSqlTestServerFixtureTestQueries.ReadOwnedValue(sibling.DataSource, ct);

        // Assert
        exists.ShouldBeFalse();
        value.ShouldBe("survives");
    }

    [Fact]
    public async Task Cancelled_creation_does_not_acquire_a_database()
    {
        // Arrange
        await using var pool = new PostgreSqlTestDatabasePool("Host=localhost;Database=postgres");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        Func<Task> create = () => pool.Create(cancellation.Token);
        _ = await create.ShouldThrowAssignableTo<OperationCanceledException>();

        // Assert
        pool.OwnedDatabaseNames.ShouldBeEmpty();
    }
}
