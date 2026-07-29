namespace SharedKernel.Npgsql.Tests;

public sealed class PostgreSqlTestServerFixtureTests(PostgreSqlTestServerFixture fixture)
{
    [Fact]
    public async Task Concurrent_database_leases_share_one_server_and_isolate_data()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var firstLease = fixture.CreateDatabase(ct);
        var secondLease = fixture.CreateDatabase(ct);
        await Task.WhenAll(firstLease, secondLease);
        await using var first = await firstLease;
        await using var second = await secondLease;

        // Act
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
    public async Task Disposing_one_database_lease_forces_cleanup_and_preserves_its_sibling()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var first = await fixture.CreateDatabase(ct);
        await using var sibling = await fixture.CreateDatabase(ct);
        await using var openConnection = await first.DataSource.OpenConnectionAsync(ct);
        var firstDatabaseName = first.DatabaseName;

        // Act
        await first.DisposeAsync();
        var firstExists = await fixture.DatabaseExists(firstDatabaseName, ct);
        var siblingExists = await fixture.DatabaseExists(sibling.DatabaseName, ct);
        await PostgreSqlTestServerFixtureTestQueries.CreateOwnedTable(sibling.DataSource, "sibling", ct);
        var siblingValue = await PostgreSqlTestServerFixtureTestQueries.ReadOwnedValue(sibling.DataSource, ct);

        // Assert
        firstExists.ShouldBeFalse();
        siblingExists.ShouldBeTrue();
        siblingValue.ShouldBe("sibling");
    }

    [Fact]
    public async Task Cancelled_test_work_does_not_prevent_database_cleanup()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var lease = await fixture.CreateDatabase(ct);
        var databaseName = lease.DatabaseName;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        Func<Task> openWithCancellation = async () =>
        {
            await using var connection = await lease.DataSource.OpenConnectionAsync(cancellation.Token);
        };
        var exception = await openWithCancellation.ShouldThrowAssignableTo<OperationCanceledException>();
        await lease.DisposeAsync();
        var databaseExists = await fixture.DatabaseExists(databaseName, ct);

        // Assert
        exception.ShouldNotBeNull();
        databaseExists.ShouldBeFalse();
    }
}
