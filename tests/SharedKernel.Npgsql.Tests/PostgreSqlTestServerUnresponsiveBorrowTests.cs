namespace PandaBear.Npgsql.Tests;

/// <summary>
/// A borrowed server that accepts connections and never answers must cost the assembly one connection timeout, not one per
/// test: the creation of every database waits for the server inside the pool's gate, so the waits would add up.
/// </summary>
public sealed class PostgreSqlTestServerUnresponsiveBorrowTests
{
    private const string Password = "unresponsive-secret";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task A_borrowed_server_that_never_answers_fails_the_first_demand_once_and_every_later_demand_from_the_cache()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var server = new UnresponsiveServer();
        var start = new ControlledStart<StartedPostgreSqlTestServer>();
        await using var source = new PostgreSqlTestServerSource(start.Start, FakeEnvironment.WithServer(server.ConnectionString(1, Password)));
        Func<Task> firstDemand = () => source.GetConnectionString(ct).WaitAsync(Patience, ct);
        Func<Task> laterDemand = () => source.GetConnectionString(ct).WaitAsync(Patience, ct);

        // Act
        var firstFailure = await firstDemand.ShouldThrowAssignableTo<Exception>();
        var laterFailures = new List<Exception>();
        for (var demand = 0; demand < 5; demand++)
        {
            laterFailures.Add(await laterDemand.ShouldThrowAssignableTo<Exception>());
        }

        // Assert
        server.Accepted.ShouldBe(1, "the first demand probes the server once, and the failure is cached for every later demand");
        laterFailures.ShouldAllSatisfy(failure => failure.ShouldBeSameAs(firstFailure, "a later demand must see the cached failure instead of waiting for the connection timeout again"));
        firstFailure.ToString().ShouldNotContain(Password, "the failure of a borrowed server must not repeat the owner's password");
        start.Calls.ShouldBe(0, "a set variable never falls back to a server of the assembly's own");
    }

    [Fact]
    public async Task Concurrent_demands_on_a_borrowed_server_that_never_answers_share_one_probe()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var server = new UnresponsiveServer();
        var start = new ControlledStart<StartedPostgreSqlTestServer>();
        await using var source = new PostgreSqlTestServerSource(start.Start, FakeEnvironment.WithServer(server.ConnectionString(1, Password)));

        // Act
        var demands = Enumerable.Range(0, 8).Select(_ => Task.Run(() => source.GetConnectionString(ct).WaitAsync(Patience, ct), ct)).ToArray();
        var failures = new List<Exception>();
        foreach (var demand in demands)
        {
            Func<Task> awaitDemand = () => demand;
            failures.Add(await awaitDemand.ShouldThrowAssignableTo<Exception>());
        }

        // Assert
        server.Accepted.ShouldBe(1, "concurrent first demands share one probe");
        failures.Distinct().Count().ShouldBe(1, "every demand sees the same failure");
    }

    [Fact]
    public async Task Every_database_request_to_a_borrowed_server_that_never_answers_fails_after_one_wait_and_registers_nothing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var server = new UnresponsiveServer();
        await using var fixture = new RefusingServerFixture(FakeEnvironment.WithServer(server.ConnectionString(1, Password)));
        Func<Task> request = () => fixture.CreateDatabase(ct).WaitAsync(Patience, ct);

        // Act
        for (var attempt = 0; attempt < 4; attempt++)
        {
            _ = await request.ShouldThrowAssignableTo<Exception>();
        }

        // Assert
        server.Accepted.ShouldBe(1, "without the cache every request waits for the timeout twice, once to create the database and once to clean up after the failure");
        fixture.OwnedDatabaseNames.ShouldBeEmpty("no request reached the server far enough to register a database");
        fixture.BuilderCalls.ShouldBe(0, "a set variable never falls back to a server of the fixture's own");
    }
}
