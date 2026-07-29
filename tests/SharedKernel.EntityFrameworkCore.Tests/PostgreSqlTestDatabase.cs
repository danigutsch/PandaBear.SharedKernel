using System.Diagnostics.CodeAnalysis;
using Npgsql;

namespace SharedKernel.EntityFrameworkCore.Tests;

internal sealed class PostgreSqlTestDatabase : IAsyncDisposable
{
    internal const string DatabaseNamePrefix = "test_";

    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(30);

    private readonly string administrativeConnectionString;
    private readonly string connectionString;
    private readonly Action<string> onDropped;
    private int disposed;

    private PostgreSqlTestDatabase(
        string administrativeConnectionString,
        string connectionString,
        string databaseName,
        Action<string> onDropped)
    {
        this.administrativeConnectionString = administrativeConnectionString;
        this.connectionString = connectionString;
        this.onDropped = onDropped;
        DatabaseName = databaseName;
    }

    internal string DatabaseName { get; }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The database name uses a fixed ASCII prefix and Guid.NewGuid().ToString(\"N\").")]
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A failed creation must independently attempt cleanup and preserve both failures.")]
    internal static async Task<PostgreSqlTestDatabase> Create(
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
            Database = "postgres",
        }.ConnectionString;
        var databaseConnectionString = new NpgsqlConnectionStringBuilder(serverConnectionString)
        {
            Database = databaseName,
        }.ConnectionString;

        try
        {
            await using var dataSource = NpgsqlDataSource.Create(administrativeConnectionString);
            await using var connection = await dataSource.OpenConnectionAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{databaseName}\";";
            _ = await command.ExecuteNonQueryAsync(ct);

            return new PostgreSqlTestDatabase(
                administrativeConnectionString,
                databaseConnectionString,
                databaseName,
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

    internal NpgsqlDataSource CreateDataSource(string name) =>
        new NpgsqlDataSourceBuilder(connectionString)
        {
            Name = name,
        }.Build();

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The database name uses a fixed ASCII prefix and Guid.NewGuid().ToString(\"N\").")]
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        await DropDatabase(administrativeConnectionString, DatabaseName).ConfigureAwait(false);
        onDropped(DatabaseName);
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The database name is issued only by this fixture from a fixed ASCII prefix and Guid.")]
    internal static async Task DropDatabase(string serverConnectionString, string databaseName)
    {
        EnsureOwnedDatabaseName(databaseName);
        var administrativeConnectionString = new NpgsqlConnectionStringBuilder(serverConnectionString)
        {
            Database = "postgres",
        }.ConnectionString;
        using var timeoutCts = new CancellationTokenSource(CleanupTimeout);
        await using var dataSource = NpgsqlDataSource.Create(administrativeConnectionString);
        await using var connection = await dataSource.OpenConnectionAsync(timeoutCts.Token).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE);";
        _ = await command.ExecuteNonQueryAsync(timeoutCts.Token).ConfigureAwait(false);
    }

    private static void EnsureOwnedDatabaseName(string databaseName)
    {
        if (!databaseName.StartsWith(DatabaseNamePrefix, StringComparison.Ordinal)
            || !Guid.TryParseExact(databaseName[DatabaseNamePrefix.Length..], "N", out _))
        {
            throw new ArgumentException("PostgreSQL test database name is not fixture-owned.", nameof(databaseName));
        }
    }
}
