namespace PandaBear.Npgsql.Tests;

/// <summary>Tests that the test server fixture starts its own server only on the first database request and caches the outcome.</summary>
public sealed class PostgreSqlTestServerFixtureLazyStartTests
{
    [Fact]
    public async Task Initializing_and_disposing_a_fixture_without_a_database_request_builds_no_server()
    {
        // Arrange
        var fixture = new RefusingServerFixture(FakeEnvironment.WithoutServer);

        // Act
        await fixture.InitializeAsync();
        await fixture.DisposeAsync();

        // Assert
        fixture.BuilderCalls.ShouldBe(0, "a test assembly whose selected tests need no database must not pay for a container");
    }

    [Fact]
    public async Task Concurrent_first_requests_build_the_server_once_and_all_see_its_failure()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var fixture = new RefusingServerFixture(FakeEnvironment.WithoutServer);
        await fixture.InitializeAsync();
        var requests = Enumerable.Range(0, 8).Select(_ => Task.Run(() => fixture.CreateDatabase(ct), ct)).ToArray();
        Func<Task> awaitRequests = () => Task.WhenAll(requests);

        // Act
        _ = await awaitRequests.ShouldThrow<InvalidOperationException>();
        await fixture.DisposeAsync();

        // Assert
        fixture.BuilderCalls.ShouldBe(1, "every concurrent request must share the first start attempt");
        requests.ShouldAllSatisfy(request => request.Exception.ShouldNotBeNull().InnerException.ShouldBeSameAs(fixture.BuilderFailure), "every request fails with the failure of the start");
    }

    [Fact]
    public async Task A_later_request_sees_the_cached_failure_without_building_again()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var fixture = new RefusingServerFixture(FakeEnvironment.WithoutServer);
        Func<Task> firstRequest = () => fixture.CreateDatabase(ct);
        Func<Task> laterRequest = () => fixture.CreateDatabase(ct);

        // Act
        var firstFailure = await firstRequest.ShouldThrow<InvalidOperationException>();
        var laterFailure = await laterRequest.ShouldThrow<InvalidOperationException>();
        await fixture.DisposeAsync();

        // Assert
        firstFailure.ShouldBeSameAs(fixture.BuilderFailure);
        laterFailure.ShouldBeSameAs(fixture.BuilderFailure, "a later request must see the cached failure instead of repeating a multi-minute startup timeout");
        fixture.BuilderCalls.ShouldBe(1);
    }

    [Fact]
    public async Task A_cancelled_request_builds_no_server_and_does_not_poison_later_requests()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var fixture = new RefusingServerFixture(FakeEnvironment.WithoutServer);
        using var cancelledCaller = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await cancelledCaller.CancelAsync();
        Func<Task> cancelledRequest = () => fixture.CreateDatabase(cancelledCaller.Token);
        Func<Task> laterRequest = () => fixture.CreateDatabase(ct);

        // Act
        _ = await cancelledRequest.ShouldThrowAssignableTo<OperationCanceledException>();
        var callsAfterCancelledRequest = fixture.BuilderCalls;
        var laterFailure = await laterRequest.ShouldThrow<InvalidOperationException>();
        await fixture.DisposeAsync();

        // Assert
        callsAfterCancelledRequest.ShouldBe(0, "a caller that gave up before asking must not cost a server");
        laterFailure.ShouldBeSameAs(fixture.BuilderFailure, "the cancelled request must not have consumed or hidden the start");
        fixture.BuilderCalls.ShouldBe(1);
    }

    [Fact]
    public async Task A_disposed_fixture_rejects_requests_without_building_a_server()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var fixture = new RefusingServerFixture(FakeEnvironment.WithoutServer);
        await fixture.DisposeAsync();
        Func<Task> request = () => fixture.CreateDatabase(ct);

        // Act
        _ = await request.ShouldThrow<ObjectDisposedException>();

        // Assert
        fixture.BuilderCalls.ShouldBe(0, "a disposed fixture has no use for a server");
    }

    [Fact]
    public async Task A_malformed_variable_fails_the_first_request_but_not_initialization_and_never_echoes_its_value()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var fixture = new RefusingServerFixture(FakeEnvironment.WithServer("Host=;Password=owner-secret"));
        Func<Task> request = () => fixture.CreateDatabase(ct);

        // Act
        await fixture.InitializeAsync();
        var failure = await request.ShouldThrow<InvalidOperationException>();
        await fixture.DisposeAsync();

        // Assert
        failure.Message.ShouldContain(PostgreSqlTestServer.ConnectionStringVariable, "the message must tell the reader which variable to fix");
        failure.ToString().ShouldNotContain("owner-secret", "the value carries the owner's password");
        fixture.BuilderCalls.ShouldBe(0, "falling back to an owned server would hide a broken owner and start a second container");
    }

    [Fact]
    public async Task A_present_but_unreachable_server_fails_every_request_with_the_connection_error_registers_nothing_and_builds_no_other_server()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var fixture = new RefusingServerFixture(FakeEnvironment.WithServer($"Host=127.0.0.1;Port={LoopbackPort.GetFree()};Username=postgres;Timeout=5"));
        Func<Task> firstRequest = () => fixture.CreateDatabase(ct);
        Func<Task> laterRequest = () => fixture.CreateDatabase(ct);

        // Act
        var firstFailure = await firstRequest.ShouldThrow<NpgsqlException>();
        var laterFailure = await laterRequest.ShouldThrow<NpgsqlException>();
        var ownedNames = fixture.OwnedDatabaseNames;
        await fixture.DisposeAsync();

        // Assert
        laterFailure.ShouldBeSameAs(firstFailure, "the server is found unreachable once, and every request sees that failure");
        ownedNames.ShouldBeEmpty("a database name is registered only after the server accepted a connection, so teardown has nothing to verify as dropped");
        fixture.BuilderCalls.ShouldBe(0, "a set variable never falls back to a server of the fixture's own");
    }
}
