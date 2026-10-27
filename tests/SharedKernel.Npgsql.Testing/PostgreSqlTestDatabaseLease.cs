using System.Diagnostics.CodeAnalysis;
using Npgsql;

namespace PandaBear.Npgsql.Testing;

/// <summary>Owns an isolated PostgreSQL test database and its default data source.</summary>
public sealed class PostgreSqlTestDatabaseLease : IAsyncDisposable
{
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(30);

    private readonly string _administrativeConnectionString;
    private readonly Action<string> _onDropped;
    private int _disposed;

    private PostgreSqlTestDatabaseLease(
        string administrativeConnectionString,
        string connectionString,
        string databaseName,
        NpgsqlDataSource dataSource,
        Action<string> onDropped)
    {
        _administrativeConnectionString = administrativeConnectionString;
        ConnectionString = connectionString;
        _onDropped = onDropped;
        DatabaseName = databaseName;
        DataSource = dataSource;
    }

    /// <summary>Gets the fixture-issued database name. </summary>
    public string DatabaseName
    {
        get;
    }

    /// <summary>Gets the isolated database connection string. </summary>
    public string ConnectionString
    {
        get;
    }

    /// <summary>Gets the lease-owned data source. </summary>
    public NpgsqlDataSource DataSource
    {
        get;
    }

    /// <summary>Creates the named database on the server and opens a data source for it, dropping the database again if any later step fails.</summary>
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The database name uses a fixed ASCII prefix and Guid.NewGuid().ToString(\"N\").")]
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Any failure after database creation must attempt independent cleanup before propagating both failures.")]
    internal static async Task<PostgreSqlTestDatabaseLease> Create(
        string serverConnectionString,
        string databaseName,
        Action<string> onDropped,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverConnectionString);
        EnsureOwnedDatabaseName(databaseName);
        ArgumentNullException.ThrowIfNull(onDropped);

        var administrativeConnectionString = new NpgsqlConnectionStringBuilder(serverConnectionString)
        {
            Database = "postgres"
        }.ConnectionString;
        var databaseConnectionString = new NpgsqlConnectionStringBuilder(serverConnectionString)
        {
            Database = databaseName
        }.ConnectionString;

        try
        {
            await using var administrativeDataSource = NpgsqlDataSource.Create(administrativeConnectionString);
            await using var connection = await administrativeDataSource.OpenConnectionAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{databaseName}\";";
            _ = await command.ExecuteNonQueryAsync(ct);

            return new PostgreSqlTestDatabaseLease(
                administrativeConnectionString,
                databaseConnectionString,
                databaseName,
                NpgsqlDataSource.Create(databaseConnectionString),
                onDropped);
        }
        catch (Exception creationFailure)
        {
            try
            {
                await DropDatabase(administrativeConnectionString, databaseName).ConfigureAwait(false);
                onDropped(databaseName);
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException(
                    "PostgreSQL test database creation and cleanup failed.",
                    creationFailure,
                    cleanupFailure);
            }

            throw;
        }
    }

    /// <summary>Creates a named data source owned by the caller.</summary>
    /// <param name="name">The diagnostic data-source name.</param>
    /// <returns>The caller-owned data source.</returns>
    public NpgsqlDataSource CreateDataSource(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new NpgsqlDataSourceBuilder(ConnectionString) { Name = name }.Build();
    }

    /// <summary>Creates a caller-owned data source with a dedicated database role.</summary>
    /// <param name="name">The diagnostic data-source name.</param>
    /// <param name="username">The database role.</param>
    /// <param name="password">The role password.</param>
    /// <returns>The caller-owned data source.</returns>
    public NpgsqlDataSource CreateDataSource(string name, string username, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(password);
        var connectionString = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Username = username,
            Password = password,
        }.ConnectionString;
        return new NpgsqlDataSourceBuilder(connectionString) { Name = name }.Build();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await PostgreSqlTestCleanup.Run(
            operationFailure: null,
            DataSource.DisposeAsync,
            async () =>
            {
                await DropDatabase(_administrativeConnectionString, DatabaseName).ConfigureAwait(false);
                _onDropped(DatabaseName);
            }).ConfigureAwait(false);
    }

    /// <summary>Force-drops the named fixture-owned database through the given server connection string, rejecting names the fixture did not issue before connecting.</summary>
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The database name is issued only by this fixture from a fixed ASCII prefix and Guid.")]
    internal static async Task DropDatabase(string serverConnectionString, string databaseName)
    {
        EnsureOwnedDatabaseName(databaseName);
        using var timeoutCts = new CancellationTokenSource(CleanupTimeout);
        await using var dataSource = NpgsqlDataSource.Create(serverConnectionString);
        await using var connection = await dataSource.OpenConnectionAsync(timeoutCts.Token).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE);";
        _ = await command.ExecuteNonQueryAsync(timeoutCts.Token).ConfigureAwait(false);
    }

    private static void EnsureOwnedDatabaseName(string databaseName)
    {
        const string prefix = "test_";
        if (!databaseName.StartsWith(prefix, StringComparison.Ordinal)
            || !Guid.TryParseExact(databaseName[prefix.Length..], "N", out _))
        {
            throw new ArgumentException("PostgreSQL test database name is not fixture-owned.", nameof(databaseName));
        }
    }
}
