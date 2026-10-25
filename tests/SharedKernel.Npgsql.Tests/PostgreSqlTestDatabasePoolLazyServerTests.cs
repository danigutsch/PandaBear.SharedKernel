namespace PandaBear.Npgsql.Tests;

public sealed class PostgreSqlTestDatabasePoolLazyServerTests(PostgreSqlTestServerFixture fixture)
{
    [Fact]
    public async Task Construction_and_disposal_without_creating_never_resolve_the_server()
    {
        // Arrange
        var resolutions = 0;
        var pool = new PostgreSqlTestDatabasePool(_ =>
        {
            resolutions++;
            throw new InvalidOperationException("The server must not be resolved.");
        });

        // Act
        await pool.DisposeAsync();

        // Assert
        resolutions.ShouldBe(0, "a pool that never created a database has no server to contact");
        pool.OwnedDatabaseNames.ShouldBeEmpty();
    }

    [Fact]
    public async Task Creation_after_disposal_is_rejected_without_resolving_the_server()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var resolutions = 0;
        var pool = new PostgreSqlTestDatabasePool(_ =>
        {
            resolutions++;
            return Task.FromResult("Host=localhost");
        });
        await pool.DisposeAsync();

        // Act
        Func<Task> create = () => pool.Create(ct);
        _ = await create.ShouldThrow<ObjectDisposedException>();

        // Assert
        resolutions.ShouldBe(0, "a disposed pool must not contact or resolve the server");
    }

    [Fact]
    public async Task Concurrent_creations_resolve_the_server_once_and_isolate_their_databases()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var bootstrap = await fixture.CreateDatabase(ct);
        var resolver = new ControlledServerResolver(bootstrap.ConnectionString);
        await using var pool = new PostgreSqlTestDatabasePool(resolver.Resolve);

        // Act
        var first = pool.Create(ct);
        await resolver.Entered;
        var second = pool.Create(ct);
        var third = pool.Create(ct);
        resolver.Release();
        var leases = await Task.WhenAll(first, second, third);
        await using var firstLease = leases[0];
        await using var secondLease = leases[1];
        await using var thirdLease = leases[2];
        await PostgreSqlTestServerFixtureTestQueries.CreateOwnedTable(firstLease.DataSource, "first", ct);
        await PostgreSqlTestServerFixtureTestQueries.CreateOwnedTable(secondLease.DataSource, "second", ct);
        var firstValue = await PostgreSqlTestServerFixtureTestQueries.ReadOwnedValue(firstLease.DataSource, ct);
        var secondValue = await PostgreSqlTestServerFixtureTestQueries.ReadOwnedValue(secondLease.DataSource, ct);

        // Assert
        resolver.Calls.ShouldBe(1, "the first successful resolution is cached for every later creation");
        leases.Select(lease => lease.DatabaseName).Distinct(StringComparer.Ordinal).Count().ShouldBe(3, "every creation gets its own database");
        firstValue.ShouldBe("first");
        secondValue.ShouldBe("second");
    }

    [Fact]
    public async Task A_failed_resolution_registers_no_database_and_the_next_creation_resolves_again()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var bootstrap = await fixture.CreateDatabase(ct);
        var resolutions = 0;
        await using var pool = new PostgreSqlTestDatabasePool(_ =>
        {
            resolutions++;
            return resolutions == 1
                ? Task.FromException<string>(new InvalidOperationException("The server is not ready."))
                : Task.FromResult(bootstrap.ConnectionString);
        });
        Func<Task> failedCreate = () => pool.Create(ct);

        // Act
        _ = await failedCreate.ShouldThrow<InvalidOperationException>();
        var namesAfterFailure = pool.OwnedDatabaseNames;
        await using var lease = await pool.Create(ct);
        var exists = await fixture.DatabaseExists(lease.DatabaseName, ct);

        // Assert
        namesAfterFailure.ShouldBeEmpty("a failed resolution must not register a database");
        resolutions.ShouldBe(2, "a failed resolution is not cached");
        exists.ShouldBeTrue();
        pool.OwnedDatabaseNames.ShouldHaveSingleItem().ShouldBe(lease.DatabaseName);
    }

    [Fact]
    public async Task A_resolution_cancelled_by_the_caller_registers_no_database_and_the_next_creation_resolves_again()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var bootstrap = await fixture.CreateDatabase(ct);
        var resolutions = 0;
        using var impatientCaller = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // A resolver that never sees the caller token is cancelled by this backstop, which fails the test
        // instead of leaving it, and the pool disposal behind it, waiting forever.
        using var backstop = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var pool = new PostgreSqlTestDatabasePool(async token =>
        {
            resolutions++;
            if (resolutions == 1)
            {
                using var observed = CancellationTokenSource.CreateLinkedTokenSource(token, backstop.Token);
                await Task.Delay(Timeout.Infinite, observed.Token);
            }

            return bootstrap.ConnectionString;
        });
        var pendingCreate = pool.Create(impatientCaller.Token);
        Func<Task> cancelledCreate = () => pendingCreate;

        // Act
        await impatientCaller.CancelAsync();
        _ = await cancelledCreate.ShouldThrowAssignableTo<OperationCanceledException>();
        var backstopFired = backstop.IsCancellationRequested;
        var namesAfterCancellation = pool.OwnedDatabaseNames;
        await using var lease = await pool.Create(ct);

        // Assert
        backstopFired.ShouldBeFalse("the resolver must observe the token of the caller that needs the server");
        namesAfterCancellation.ShouldBeEmpty("a cancelled resolution must not register a database");
        resolutions.ShouldBe(2, "a cancelled resolution is not cached");
        pool.OwnedDatabaseNames.ShouldHaveSingleItem().ShouldBe(lease.DatabaseName);
    }

    [Fact]
    public async Task A_resolver_without_a_connection_string_fails_the_creation_and_registers_no_database()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var pool = new PostgreSqlTestDatabasePool(_ => Task.FromResult(" "));
        Func<Task> create = () => pool.Create(ct);

        // Act
        _ = await create.ShouldThrow<InvalidOperationException>();

        // Assert
        pool.OwnedDatabaseNames.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_lease_from_a_lazy_pool_is_isolated_and_dropped_on_disposal_while_a_sibling_survives()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var bootstrap = await fixture.CreateDatabase(ct);
        await using var pool = new PostgreSqlTestDatabasePool(_ => Task.FromResult(bootstrap.ConnectionString));
        await using var siblingPool = new PostgreSqlTestDatabasePool(bootstrap.ConnectionString);
        await using var lease = await pool.Create(ct);
        await using var sibling = await siblingPool.Create(ct);
        var databaseName = lease.DatabaseName;
        await PostgreSqlTestServerFixtureTestQueries.CreateOwnedTable(lease.DataSource, "lazy", ct);

        // Act
        await pool.DisposeAsync();
        var exists = await fixture.DatabaseExists(databaseName, ct);
        await PostgreSqlTestServerFixtureTestQueries.CreateOwnedTable(sibling.DataSource, "survives", ct);
        var siblingValue = await PostgreSqlTestServerFixtureTestQueries.ReadOwnedValue(sibling.DataSource, ct);

        // Assert
        exists.ShouldBeFalse("disposing the lazy pool must drop its outstanding database");
        pool.OwnedDatabaseNames.ShouldBeEmpty();
        siblingValue.ShouldBe("survives");
    }

    [Fact]
    public void A_resolver_is_required()
    {
        // Arrange
        Func<object> construct = () => new PostgreSqlTestDatabasePool((Func<CancellationToken, Task<string>>)null!);

        // Act
        var failure = construct.ShouldThrow<ArgumentNullException>();

        // Assert
        failure.ParamName.ShouldBe("resolveServerConnectionString", "the argument name is part of the constructor contract");
    }

    [Fact]
    public void A_null_literal_still_binds_to_the_connection_string_constructor()
    {
        // Arrange
        Func<object> construct = () => new PostgreSqlTestDatabasePool(null!);

        // Act
        var failure = construct.ShouldThrow<ArgumentNullException>();

        // Assert
        failure.ParamName.ShouldBe("serverConnectionString", "a call that compiled before the resolver overload existed must keep its meaning");
    }

    [Fact]
    public void The_connection_string_constructor_still_validates_its_argument_eagerly()
    {
        // Arrange
        Func<object> constructBlank = () => new PostgreSqlTestDatabasePool(" ");
        Func<object> constructMalformed = () => new PostgreSqlTestDatabasePool("not a connection string");

        // Act
        var blank = constructBlank.ShouldThrow<ArgumentException>();
        _ = constructMalformed.ShouldThrow<ArgumentException>();

        // Assert
        blank.ParamName.ShouldBe("serverConnectionString", "the argument name is part of the constructor contract");
    }
}
