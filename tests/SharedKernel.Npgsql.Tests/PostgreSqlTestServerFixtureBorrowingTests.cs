namespace PandaBear.Npgsql.Tests;

/// <summary>Tests fixtures that borrow an already running PostgreSQL server named in the environment instead of starting one.</summary>
/// <param name="fixture">The shared PostgreSQL test server fixture whose server the borrowing fixtures connect to.</param>
public sealed class PostgreSqlTestServerFixtureBorrowingTests(PostgreSqlTestServerFixture fixture)
{
    [Fact]
    public async Task A_borrowing_fixture_leases_isolated_databases_on_the_borrowed_server_without_building_one()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var server = await fixture.GetServerConnectionString(ct);
        await using var borrower = new RefusingServerFixture(FakeEnvironment.WithServer(server));
        await borrower.InitializeAsync();
        await using var reference = await fixture.CreateDatabase(ct);

        // Act
        var leases = await Task.WhenAll(borrower.CreateDatabase(ct), borrower.CreateDatabase(ct));
        await using var first = leases[0];
        await using var second = leases[1];
        var borrowedServer = await PostgreSqlTestServerFixtureTestQueries.GetServerIdentity(first.DataSource, ct);
        var referenceServer = await PostgreSqlTestServerFixtureTestQueries.GetServerIdentity(reference.DataSource, ct);
        await PostgreSqlTestServerFixtureTestQueries.CreateOwnedTable(first.DataSource, "first", ct);
        await PostgreSqlTestServerFixtureTestQueries.CreateOwnedTable(second.DataSource, "second", ct);
        var firstValue = await PostgreSqlTestServerFixtureTestQueries.ReadOwnedValue(first.DataSource, ct);
        var secondValue = await PostgreSqlTestServerFixtureTestQueries.ReadOwnedValue(second.DataSource, ct);
        var firstExists = await borrower.DatabaseExists(first.DatabaseName, ct);
        await borrower.DisposeAsync();

        // Assert
        borrower.BuilderCalls.ShouldBe(0, "a borrowing fixture never starts a server");
        borrowedServer.ShouldBe(referenceServer, "the lease lives on the server that the variable names");
        first.DatabaseName.ShouldNotBe(second.DatabaseName);
        firstValue.ShouldBe("first");
        secondValue.ShouldBe("second");
        firstExists.ShouldBeTrue();
    }

    [Fact]
    public async Task Disposing_a_borrowing_fixture_drops_only_its_databases_and_leaves_the_server_and_its_siblings_running()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var server = await fixture.GetServerConnectionString(ct);
        await using var borrower = new RefusingServerFixture(FakeEnvironment.WithServer(server));
        await using var siblingBorrower = new RefusingServerFixture(FakeEnvironment.WithServer(server));
        await using var assemblyDatabase = await fixture.CreateDatabase(ct);
        var borrowed = await borrower.CreateDatabase(ct);
        await using var sibling = await siblingBorrower.CreateDatabase(ct);
        await PostgreSqlTestServerFixtureTestQueries.CreateOwnedTable(sibling.DataSource, "sibling", ct);
        var borrowedName = borrowed.DatabaseName;

        // Act
        await borrower.DisposeAsync();
        var borrowedExists = await fixture.DatabaseExists(borrowedName, ct);
        var siblingExists = await fixture.DatabaseExists(sibling.DatabaseName, ct);
        var siblingValue = await PostgreSqlTestServerFixtureTestQueries.ReadOwnedValue(sibling.DataSource, ct);
        await using var created = await fixture.CreateDatabase(ct);
        var createdExists = await fixture.DatabaseExists(created.DatabaseName, ct);
        await siblingBorrower.DisposeAsync();

        // Assert
        borrowedExists.ShouldBeFalse("the borrower drops the databases it issued");
        siblingExists.ShouldBeTrue("a sibling's database survives another borrower's disposal");
        siblingValue.ShouldBe("sibling");
        createdExists.ShouldBeTrue("the server keeps serving the assembly fixture after a borrower is gone");
        borrower.BuilderCalls.ShouldBe(0, "a borrowing fixture has no server of its own to stop");
        siblingBorrower.BuilderCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Several_borrowers_create_and_drop_databases_concurrently_in_isolation_and_the_server_survives()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var server = await fixture.GetServerConnectionString(ct);
        var borrowers = Enumerable.Range(0, 4).Select(_ => new RefusingServerFixture(FakeEnvironment.WithServer(server))).ToArray();

        // Act
        var scenarios = borrowers.Select(async (borrower, index) =>
        {
            var lease = await borrower.CreateDatabase(ct);
            await PostgreSqlTestServerFixtureTestQueries.CreateOwnedTable(lease.DataSource, $"borrower-{index}", ct);
            return (lease.DatabaseName, Value: await PostgreSqlTestServerFixtureTestQueries.ReadOwnedValue(lease.DataSource, ct));
        });
        var outcomes = await Task.WhenAll(scenarios);
        await Task.WhenAll(borrowers.Select(borrower => borrower.DisposeAsync().AsTask()));
        var survivors = await Task.WhenAll(outcomes.Select(outcome => fixture.DatabaseExists(outcome.DatabaseName, ct)));
        await using var afterwards = await fixture.CreateDatabase(ct);
        var afterwardsExists = await fixture.DatabaseExists(afterwards.DatabaseName, ct);

        // Assert
        outcomes.Select(outcome => outcome.Value).ShouldBe(["borrower-0", "borrower-1", "borrower-2", "borrower-3"], "every borrower sees only its own data");
        outcomes.Select(outcome => outcome.DatabaseName).Distinct(StringComparer.Ordinal).Count().ShouldBe(4, "every borrower gets its own database");
        survivors.ShouldAllSatisfy(exists => exists.ShouldBeFalse(), "every borrower dropped its database");
        borrowers.ShouldAllSatisfy(borrower => borrower.BuilderCalls.ShouldBe(0), "no borrower built a server");
        afterwardsExists.ShouldBeTrue("the server survives all of them");
    }

    [Fact]
    public async Task A_database_that_cannot_be_dropped_stays_registered_and_is_reported_until_a_retry_succeeds()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var server = await fixture.GetServerConnectionString(ct);
        await using var borrower = new RefusingServerFixture(FakeEnvironment.WithServer(server));
        var lease = await borrower.CreateDatabase(ct);
        var databaseName = lease.DatabaseName;
        await PostgreSqlTestServerAdministration.SetTemplateFlag(server, databaseName, isTemplate: true, ct);
        Func<Task> firstDisposal = () => borrower.DisposeAsync().AsTask();

        // Act
        var failure = await firstDisposal.ShouldThrow<AggregateException>();
        var namesAfterFailure = borrower.OwnedDatabaseNames;
        var existsAfterFailure = await fixture.DatabaseExists(databaseName, ct);
        await PostgreSqlTestServerAdministration.SetTemplateFlag(server, databaseName, isTemplate: false, ct);
        await borrower.DisposeAsync();
        var existsAfterRetry = await fixture.DatabaseExists(databaseName, ct);

        // Assert
        failure.Message.ShouldStartWith("PostgreSQL test server cleanup failed.", "the fixture reports its cleanup failures in one aggregate");
        ExceptionLeaves.Of(failure).ShouldHaveSingleItem().ShouldBeAssignableTo<PostgresException>("the drop of a template database is refused by the server");
        namesAfterFailure.ShouldHaveSingleItem().ShouldBe(databaseName);
        existsAfterFailure.ShouldBeTrue("a failed drop must not be mistaken for a clean teardown");
        existsAfterRetry.ShouldBeFalse("the retained name lets a later disposal finish the cleanup");
        borrower.OwnedDatabaseNames.ShouldBeEmpty();
    }

    [Fact]
    public async Task Disposing_a_borrowing_fixture_forces_the_drop_of_a_database_with_an_open_connection()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var server = await fixture.GetServerConnectionString(ct);
        await using var borrower = new RefusingServerFixture(FakeEnvironment.WithServer(server));
        var lease = await borrower.CreateDatabase(ct);
        await using var openConnection = await lease.DataSource.OpenConnectionAsync(ct);
        var databaseName = lease.DatabaseName;

        // Act
        await borrower.DisposeAsync();
        var exists = await fixture.DatabaseExists(databaseName, ct);

        // Assert
        exists.ShouldBeFalse("DROP DATABASE ... WITH (FORCE) must end the open session");
        borrower.OwnedDatabaseNames.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_cancelled_creation_on_a_borrowed_server_registers_no_database_and_later_creations_work()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var server = await fixture.GetServerConnectionString(ct);
        await using var borrower = new RefusingServerFixture(FakeEnvironment.WithServer(server));
        using var cancelledCaller = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await cancelledCaller.CancelAsync();
        Func<Task> cancelledCreation = () => borrower.CreateDatabase(cancelledCaller.Token);

        // Act
        _ = await cancelledCreation.ShouldThrowAssignableTo<OperationCanceledException>();
        var namesAfterCancellation = borrower.OwnedDatabaseNames;
        var lease = await borrower.CreateDatabase(ct);
        var exists = await fixture.DatabaseExists(lease.DatabaseName, ct);
        await borrower.DisposeAsync();

        // Assert
        namesAfterCancellation.ShouldBeEmpty("a cancelled creation must not register a database");
        exists.ShouldBeTrue();
        borrower.BuilderCalls.ShouldBe(0);
    }
}
