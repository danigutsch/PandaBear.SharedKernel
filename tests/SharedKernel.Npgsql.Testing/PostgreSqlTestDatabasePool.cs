using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Npgsql;

namespace PandaBear.Npgsql.Testing;

/// <summary>Issues isolated test databases on a caller-owned PostgreSQL server.</summary>
[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "The semaphore never allocates a wait handle and must remain available for concurrent or retrying disposal calls.")]
public sealed class PostgreSqlTestDatabasePool : IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly ConcurrentDictionary<string, PostgreSqlTestDatabaseLease?> _databases = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    /// <summary>Creates a pool without starting or taking ownership of the server.</summary>
    /// <param name="serverConnectionString">A connection string with permission to create databases.</param>
    public PostgreSqlTestDatabasePool(string serverConnectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverConnectionString);
        _connectionString = new NpgsqlConnectionStringBuilder(serverConnectionString) { Database = "postgres" }.ConnectionString;
    }

    /// <summary>Gets a snapshot of databases awaiting successful cleanup.</summary>
    public IReadOnlyCollection<string> OwnedDatabaseNames => Array.AsReadOnly(_databases.Keys.ToArray());

    /// <summary>Creates a new isolated database with an internally generated name.</summary>
    /// <param name="ct">Cancels acquisition without cancelling subsequent cleanup.</param>
    /// <returns>A lease owned by the caller and tracked until successful cleanup.</returns>
    public async Task<PostgreSqlTestDatabaseLease> Create(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var databaseName = $"test_{Guid.NewGuid():N}";
            if (!_databases.TryAdd(databaseName, null))
            {
                throw new InvalidOperationException("A duplicate PostgreSQL test database was registered.");
            }

            var lease = await PostgreSqlTestDatabaseLease.Create(_connectionString, databaseName, OnDropped, ct).ConfigureAwait(false);
            _databases[databaseName] = lease;
            return lease;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <inheritdoc />
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Teardown must attempt every owned database before reporting failures.")]
    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _disposed = true;
            List<Exception> failures = [];
            foreach (var (name, lease) in _databases.ToArray())
            {
                try
                {
                    if (lease is not null)
                    {
                        await lease.DisposeAsync().ConfigureAwait(false);
                    }

                    // A failed lease disposal remains registered, even when it was already attempted.
                    if (_databases.ContainsKey(name))
                    {
                        await PostgreSqlTestDatabaseLease.DropDatabase(_connectionString, name).ConfigureAwait(false);
                        OnDropped(name);
                    }
                }
                catch (Exception failure)
                {
                    failures.Add(failure);
                }
            }

            if (failures.Count != 0)
            {
                throw new AggregateException("PostgreSQL test database pool cleanup failed.", failures);
            }
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    private void OnDropped(string name)
    {
        _ = _databases.TryRemove(name, out _);
    }
}
