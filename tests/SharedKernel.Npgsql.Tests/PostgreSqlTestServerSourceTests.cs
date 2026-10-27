namespace PandaBear.Npgsql.Tests;

/// <summary>Tests PostgreSqlTestServerSource, which starts an owned server on first demand or borrows one named in the environment.</summary>
public sealed class PostgreSqlTestServerSourceTests
{
    [Fact]
    public async Task A_server_is_started_on_the_first_demand_and_not_before()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var start = new ControlledStart<StartedPostgreSqlTestServer>();
        await using var source = new PostgreSqlTestServerSource(start.Start, FakeEnvironment.WithoutServer);
        var callsBeforeDemand = start.Calls;

        // Act
        var demand = source.GetConnectionString(ct);
        var callsWhenDemandReturned = start.Calls;
        start.Complete(new StartedPostgreSqlTestServer("Host=owned", () => ValueTask.CompletedTask));
        var connectionString = await demand;

        // Assert
        callsBeforeDemand.ShouldBe(0, "constructing the source must not start anything");
        callsWhenDemandReturned.ShouldBe(1, "the first demand starts the server on the caller's thread, where Aspire finds its metadata");
        connectionString.ShouldBe("Host=owned");
    }

    [Fact]
    public async Task Concurrent_first_demands_start_one_server_and_share_its_connection_string()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var start = new ControlledStart<StartedPostgreSqlTestServer>();
        await using var source = new PostgreSqlTestServerSource(start.Start, FakeEnvironment.WithoutServer);

        // Act
        var demands = Enumerable.Range(0, 16).Select(_ => Task.Run(() => source.GetConnectionString(ct), ct)).ToArray();
        start.Complete(new StartedPostgreSqlTestServer("Host=owned", () => ValueTask.CompletedTask));
        var connectionStrings = await Task.WhenAll(demands);

        // Assert
        start.Calls.ShouldBe(1, "every concurrent demand must share the first start");
        connectionStrings.ShouldAllSatisfy(connectionString => connectionString.ShouldBe("Host=owned"), "every demand must receive the shared server");
    }

    [Fact]
    public async Task A_demand_abandoned_by_its_caller_leaves_the_start_to_the_other_demands()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var start = new ControlledStart<StartedPostgreSqlTestServer>();

        // Disposed explicitly below: disposal waits for the start, which only completes after the wait that follows.
        var source = new PostgreSqlTestServerSource(start.Start, FakeEnvironment.WithoutServer);
        using var impatientCaller = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var abandoned = source.GetConnectionString(impatientCaller.Token);
        var patient = source.GetConnectionString(ct);

        // Act
        await impatientCaller.CancelAsync();

        // The start completes only after this wait, so a demand that is not abandoned must fail the test, not hang it.
        Func<Task> awaitAbandoned = () => abandoned.WaitAsync(TimeSpan.FromSeconds(30));
        _ = await awaitAbandoned.ShouldThrowAssignableTo<OperationCanceledException>();
        var startTokenCancelled = start.Token.IsCancellationRequested;
        start.Complete(new StartedPostgreSqlTestServer("Host=owned", () => ValueTask.CompletedTask));
        var patientValue = await patient;
        await source.DisposeAsync();

        // Assert
        startTokenCancelled.ShouldBeFalse("a caller's token only abandons that caller's wait, so the start never sees it");
        start.Token.ShouldNotBe(impatientCaller.Token, "the start must run with a token owned by the source, not a test's token");
        patientValue.ShouldBe("Host=owned");
        start.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task A_demand_that_is_already_cancelled_starts_nothing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var start = new ControlledStart<StartedPostgreSqlTestServer>();

        // Disposed explicitly below: a source that started a server for a cancelled caller could not be disposed before the test failed.
        var source = new PostgreSqlTestServerSource(start.Start, FakeEnvironment.WithoutServer);
        using var cancelledCaller = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await cancelledCaller.CancelAsync();

        // A source that ignores the cancelled caller would wait for a start that never completes: fail instead of hang.
        Func<Task> demand = () => source.GetConnectionString(cancelledCaller.Token).WaitAsync(TimeSpan.FromSeconds(30), ct);

        // Act
        _ = await demand.ShouldThrowAssignableTo<OperationCanceledException>();
        await source.DisposeAsync();

        // Assert
        start.Calls.ShouldBe(0, "a caller that gave up before asking must not cost a server");
    }

    [Fact]
    public async Task A_failed_start_is_cached_and_every_later_demand_sees_the_same_failure()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var failure = new InvalidOperationException("startup failed");
        var start = new ControlledStart<StartedPostgreSqlTestServer>();
        await using var source = new PostgreSqlTestServerSource(start.Start, FakeEnvironment.WithoutServer);
        var firstDemand = source.GetConnectionString(ct);

        // Act
        start.Fail(failure);
        Func<Task> awaitFirst = () => firstDemand;
        Func<Task> awaitLater = () => source.GetConnectionString(ct);
        var firstFailure = await awaitFirst.ShouldThrow<InvalidOperationException>();
        var laterFailure = await awaitLater.ShouldThrow<InvalidOperationException>();

        // Assert
        firstFailure.ShouldBeSameAs(failure);
        laterFailure.ShouldBeSameAs(failure, "a later demand must see the cached failure instead of repeating a multi-minute startup timeout");
        start.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task A_present_variable_is_borrowed_and_no_server_is_started_or_stopped()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var start = new ControlledStart<StartedPostgreSqlTestServer>();
        List<string> reached = [];
        var source = new PostgreSqlTestServerSource(
            start.Start,
            FakeEnvironment.WithServer("Host=owner;Port=41234;Password=owner-secret"),
            (server, _) =>
            {
                reached.Add(server);
                return Task.CompletedTask;
            });

        // Act
        // A source that starts a server instead of borrowing one would wait for a start that never completes: fail instead of hang.
        var connectionString = await source.GetConnectionString(ct).WaitAsync(TimeSpan.FromSeconds(30), ct);
        var laterConnectionString = await source.GetConnectionString(ct).WaitAsync(TimeSpan.FromSeconds(30), ct);
        await source.DisposeAsync();

        // Assert
        connectionString.ShouldBe("Host=owner;Port=41234;Password=owner-secret");
        laterConnectionString.ShouldBe(connectionString);
        reached.ShouldBe(["Host=owner;Port=41234;Password=owner-secret"], "the first demand connects to the borrowed server once and no later demand does");
        start.Calls.ShouldBe(0, "a borrowed server belongs to its owner, so the source never starts one");
    }

    [Fact]
    public async Task A_borrowed_server_that_rejects_the_first_connection_fails_every_demand_with_that_failure_and_starts_no_server()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var failure = new InvalidOperationException("the owner refused the connection");
        var attempts = 0;
        var start = new ControlledStart<StartedPostgreSqlTestServer>();
        await using var source = new PostgreSqlTestServerSource(
            start.Start,
            FakeEnvironment.WithServer("Host=owner;Port=41234;Password=owner-secret"),
            (_, _) =>
            {
                attempts++;
                return Task.FromException(failure);
            });
        Func<Task> firstDemand = () => source.GetConnectionString(ct).WaitAsync(TimeSpan.FromSeconds(30), ct);
        Func<Task> laterDemand = () => source.GetConnectionString(ct).WaitAsync(TimeSpan.FromSeconds(30), ct);

        // Act
        var firstFailure = await firstDemand.ShouldThrow<InvalidOperationException>();
        var laterFailure = await laterDemand.ShouldThrow<InvalidOperationException>();

        // Assert
        firstFailure.ShouldBeSameAs(failure);
        laterFailure.ShouldBeSameAs(failure, "a later demand must see the cached failure instead of connecting again");
        attempts.ShouldBe(1);
        start.Calls.ShouldBe(0, "a set variable never falls back to a server of the source's own");
    }

    [Fact]
    public async Task A_malformed_variable_fails_every_demand_and_never_falls_back_to_an_owned_server()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var start = new ControlledStart<StartedPostgreSqlTestServer>();

        // Disposed explicitly below: a source that fell back to a pending start could not be disposed before the test failed.
        var source = new PostgreSqlTestServerSource(start.Start, FakeEnvironment.WithServer("not a connection string"));

        // A source that falls back to a server of its own would wait for a start that never completes: fail instead of hang.
        Func<Task> firstDemand = () => source.GetConnectionString(ct).WaitAsync(TimeSpan.FromSeconds(30), ct);
        Func<Task> secondDemand = () => source.GetConnectionString(ct).WaitAsync(TimeSpan.FromSeconds(30), ct);

        // Act
        var firstFailure = await firstDemand.ShouldThrow<InvalidOperationException>();
        var secondFailure = await secondDemand.ShouldThrow<InvalidOperationException>();
        await source.DisposeAsync();

        // Assert
        firstFailure.Message.ShouldContain(PostgreSqlTestServer.ConnectionStringVariable, "the failure must name the variable to fix");
        secondFailure.Message.ShouldContain(PostgreSqlTestServer.ConnectionStringVariable, "the failure is the same on every demand");
        start.Calls.ShouldBe(0, "falling back to an owned server would hide a broken owner and start a second container");
    }

    [Fact]
    public async Task Disposal_stops_the_server_that_the_source_started_exactly_once()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var stops = 0;
        var start = new ControlledStart<StartedPostgreSqlTestServer>();
        var source = new PostgreSqlTestServerSource(start.Start, FakeEnvironment.WithoutServer);
        var demand = source.GetConnectionString(ct);
        start.Complete(new StartedPostgreSqlTestServer("Host=owned", () =>
        {
            stops++;
            return ValueTask.CompletedTask;
        }));
        _ = await demand;

        // Act
        await source.DisposeAsync();
        await source.DisposeAsync();

        // Assert
        stops.ShouldBe(1, "the owner stops its server once, however often the source is disposed");
    }

    [Fact]
    public async Task Disposal_without_a_demand_never_starts_a_server()
    {
        // Arrange
        var start = new ControlledStart<StartedPostgreSqlTestServer>();
        var source = new PostgreSqlTestServerSource(start.Start, FakeEnvironment.WithoutServer);

        // Act
        // A disposal that starts the server and waits for it must fail this test instead of hanging it.
        await source.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        // Assert
        start.Calls.ShouldBe(0, "an assembly without a database test must not pay for a container");
    }
}
