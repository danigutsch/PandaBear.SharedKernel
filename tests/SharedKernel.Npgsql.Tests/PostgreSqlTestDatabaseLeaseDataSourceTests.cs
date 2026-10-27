namespace PandaBear.Npgsql.Tests;

/// <summary>Tests the extra data sources that a database lease can create for its database.</summary>
/// <param name="fixture">The shared PostgreSQL test server fixture that leases the test databases.</param>
public sealed class PostgreSqlTestDatabaseLeaseDataSourceTests(PostgreSqlTestServerFixture fixture)
{
    [Fact]
    public async Task A_named_data_source_reaches_the_lease_database_and_belongs_to_the_caller()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var lease = await fixture.CreateDatabase(ct);
        var dataSource = lease.CreateDataSource("named-source");

        // Act
        var session = await PostgreSqlSessionQueries.GetSession(dataSource, ct);
        await dataSource.DisposeAsync();
        var leaseSession = await PostgreSqlSessionQueries.GetSession(lease.DataSource, ct);

        // Assert
        dataSource.ShouldNotBeSameAs(lease.DataSource, "the caller disposes the returned source, so it must not be the lease's own source");
        session.Database.ShouldBe(lease.DatabaseName, "the source connects to the lease database");
        leaseSession.Database.ShouldBe(lease.DatabaseName, "disposing the caller's source leaves the lease's own source usable");
    }

    [Fact]
    public async Task A_role_data_source_connects_to_the_lease_database_as_that_role()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var bootstrap = await fixture.CreateDatabase(ct);
        await using var role = await TemporaryPostgreSqlRole.Create(bootstrap.DataSource, ct);
        await using var lease = await fixture.CreateDatabase(ct);
        await using var dataSource = lease.CreateDataSource("role-source", role.Name, role.Password);

        // Act
        var session = await PostgreSqlSessionQueries.GetSession(dataSource, ct);
        var leaseSession = await PostgreSqlSessionQueries.GetSession(lease.DataSource, ct);

        // Assert
        session.Database.ShouldBe(lease.DatabaseName, "the source connects to the lease database");
        session.User.ShouldBe(role.Name, "the source authenticates with the given role and password");
        leaseSession.User.ShouldNotBe(role.Name, "the lease's own source keeps the server credentials");
    }

    [Fact]
    public async Task Disposing_the_lease_forces_the_drop_while_a_named_data_source_connection_is_open()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var lease = await fixture.CreateDatabase(ct);
        await using var dataSource = lease.CreateDataSource("open-connection");
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var databaseName = lease.DatabaseName;

        // Act
        await lease.DisposeAsync();
        var databaseExists = await fixture.DatabaseExists(databaseName, ct);

        // Assert
        databaseExists.ShouldBeFalse("a connection from a caller-owned source must not keep the database alive");
    }

    [Fact]
    public async Task Disposing_the_lease_forces_the_drop_while_a_role_data_source_connection_is_open()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var bootstrap = await fixture.CreateDatabase(ct);
        await using var role = await TemporaryPostgreSqlRole.Create(bootstrap.DataSource, ct);
        var lease = await fixture.CreateDatabase(ct);
        await using var dataSource = lease.CreateDataSource("open-role-connection", role.Name, role.Password);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var databaseName = lease.DatabaseName;

        // Act
        await lease.DisposeAsync();
        var databaseExists = await fixture.DatabaseExists(databaseName, ct);

        // Assert
        databaseExists.ShouldBeFalse("a connection from a role source must not keep the database alive");
    }
}
