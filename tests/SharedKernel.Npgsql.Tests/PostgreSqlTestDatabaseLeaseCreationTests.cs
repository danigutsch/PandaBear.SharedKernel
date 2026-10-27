using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace PandaBear.Npgsql.Tests;

/// <summary>Tests creating a test database lease directly, including cancellation, failed cleanup and name validation.</summary>
/// <param name="fixture">The shared PostgreSQL test server fixture that leases the test databases.</param>
public sealed class PostgreSqlTestDatabaseLeaseCreationTests(PostgreSqlTestServerFixture fixture)
{
    // The pool registers a name before it creates the database and unregisters it through onDropped. The existing pool
    // test cancels before the pool takes its gate, so no name is registered and the cleanup after a registered creation
    // was cancelled never runs. This test registers the name first, exactly as the pool does, and then cancels.
    [Fact]
    public async Task Cancelled_creation_of_a_registered_name_reports_the_drop_once_and_leaves_no_database()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var bootstrap = await fixture.CreateDatabase(ct);
        var databaseName = $"test_{Guid.NewGuid():N}";
        var registeredNames = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        var droppedNames = new ConcurrentQueue<string>();
        _ = registeredNames.TryAdd(databaseName, 0);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        Func<Task> create = () => PostgreSqlTestDatabaseLease.Create(
            bootstrap.ConnectionString,
            databaseName,
            name =>
            {
                droppedNames.Enqueue(name);
                _ = registeredNames.TryRemove(name, out _);
            },
            cancellation.Token);
        _ = await create.ShouldThrowAssignableTo<OperationCanceledException>();
        var databaseExists = await fixture.DatabaseExists(databaseName, ct);
        await using var next = await fixture.CreateDatabase(ct);
        var bootstrapServer = await PostgreSqlTestServerFixtureTestQueries.GetServerIdentity(bootstrap.DataSource, ct);
        var nextServer = await PostgreSqlTestServerFixtureTestQueries.GetServerIdentity(next.DataSource, ct);

        // Assert
        droppedNames.ShouldHaveSingleItem("the cleanup after a cancelled creation reports the drop exactly once").ShouldBe(databaseName);
        registeredNames.ShouldBeEmpty("the registered name is released once its database is gone");
        databaseExists.ShouldBeFalse("a cancelled creation must not leave its database behind");
        nextServer.ShouldBe(bootstrapServer, "the server and its sibling database keep working after the cancelled creation");
    }

    // A drop that fails must leave the name registered so that its owner can retry the cleanup. No server is reachable
    // here, so the creation and the cleanup both fail.
    [Fact]
    public async Task Failed_cleanup_after_a_failed_creation_keeps_the_name_registered_and_reports_both_failures()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var databaseName = $"test_{Guid.NewGuid():N}";
        var droppedNames = new ConcurrentQueue<string>();
        using var unusedListener = new TcpListener(IPAddress.Loopback, 0);
        unusedListener.Start();
        var closedPort = ((IPEndPoint)unusedListener.LocalEndpoint).Port;
        unusedListener.Stop();
        var unreachableServer = $"Host=127.0.0.1;Port={closedPort};Username=nobody;Timeout=5";

        // Act
        Func<Task> create = () => PostgreSqlTestDatabaseLease.Create(unreachableServer, databaseName, droppedNames.Enqueue, ct);
        var exception = await create.ShouldThrow<AggregateException>();

        // Assert
        exception.InnerExceptions.Count.ShouldBe(2, "the creation failure and the cleanup failure are both reported");
        exception.InnerExceptions.ShouldAllSatisfy(inner => inner.ShouldBeAssignableTo<NpgsqlException>(), "both failures come from the unreachable server");
        droppedNames.ShouldBeEmpty("a name whose database could not be dropped stays registered for the owner's retry");
    }

    [Theory]
    [InlineData("postgres")]
    [InlineData("test_not-a-guid")]
    [InlineData("test_00000000000000000000000000000000\"; DROP DATABASE postgres; --")]
    public async Task Creation_rejects_non_fixture_owned_names_before_connecting(string databaseName)
    {
        // Arrange
        const string invalidConnectionString = "not-a-connection-string";
        var droppedNames = new ConcurrentQueue<string>();

        // Act
        Func<Task> create = () => PostgreSqlTestDatabaseLease.Create(
            invalidConnectionString,
            databaseName,
            droppedNames.Enqueue,
            TestContext.Current.CancellationToken);
        var exception = await create.ShouldThrow<ArgumentException>();

        // Assert
        exception.ParamName.ShouldBe("databaseName", "the name guard must run before the connection string is parsed or any server is contacted");
        droppedNames.ShouldBeEmpty("a rejected name was never registered, so nothing is dropped");
    }
}
