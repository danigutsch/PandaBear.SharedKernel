using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Npgsql;

namespace PandaBear.Npgsql.Testing;

/// <summary>Issues isolated test databases on a caller-owned PostgreSQL server.</summary>
[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "The semaphore never allocates a wait handle and must remain available for concurrent or retrying disposal calls.")]
public sealed class PostgreSqlTestDatabasePool : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task<string>> _resolveServerConnectionString;
    private readonly ConcurrentDictionary<string, PostgreSqlTestDatabaseLease?> _databases = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _connectionString;
    private bool _disposed;

    /// <summary>Creates a pool without starting or taking ownership of the server.</summary>
    /// <param name="serverConnectionString">A connection string with permission to create databases.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="serverConnectionString"/> is <see langword="null"/>, blank, or not a connection string.
    /// </exception>
    [OverloadResolutionPriority(1)] // A null literal keeps binding here, as it did before the resolver overload existed.
    public PostgreSqlTestDatabasePool(string serverConnectionString)
        : this(CreateFixedResolver(serverConnectionString))
    {
    }

    /// <summary>
    /// Creates a pool that resolves the server connection string when the first database is created,
    /// without starting or taking ownership of the server.
    /// </summary>
    /// <param name="resolveServerConnectionString">
    /// Returns a connection string with permission to create databases. It runs inside the pool's gate with the
    /// token of the caller that needs it. The first successful result is kept; a failed or cancelled
    /// resolution registers nothing and the next <see cref="Create"/> resolves again. A pool that never
    /// created a database never calls it. A <see langword="null"/> or blank result is a defect of the resolver:
    /// <see cref="Create"/> fails with <see cref="InvalidOperationException"/> rather than waiting for a server,
    /// so a resolver that is not ready yet must wait or throw. Other failures of the resolver reach the caller
    /// of <see cref="Create"/> unchanged.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="resolveServerConnectionString"/> is <see langword="null"/>.
    /// </exception>
    public PostgreSqlTestDatabasePool(Func<CancellationToken, Task<string>> resolveServerConnectionString)
    {
        ArgumentNullException.ThrowIfNull(resolveServerConnectionString);
        _resolveServerConnectionString = resolveServerConnectionString;
    }

    /// <summary>Gets a snapshot of databases awaiting successful cleanup.</summary>
    public IReadOnlyCollection<string> OwnedDatabaseNames => Array.AsReadOnly(_databases.Keys.ToArray());

    /// <summary>Creates a new isolated database with an internally generated name.</summary>
    /// <param name="ct">Cancels acquisition without cancelling subsequent cleanup.</param>
    /// <returns>A lease owned by the caller and tracked until successful cleanup.</returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="ct"/> is cancelled while the caller waits for the pool, while the server connection
    /// string is resolved, or while the database is created.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when the pool is disposed; the server is not resolved.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the resolver returns <see langword="null"/> or a blank connection string; no database is registered
    /// and the next call resolves again.
    /// </exception>
    public async Task<PostgreSqlTestDatabaseLease> Create(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var connectionString = await ResolveConnectionString(ct).ConfigureAwait(false);
            var databaseName = $"test_{Guid.NewGuid():N}";
            if (!_databases.TryAdd(databaseName, null))
            {
                throw new InvalidOperationException("A duplicate PostgreSQL test database was registered.");
            }

            var lease = await PostgreSqlTestDatabaseLease.Create(connectionString, databaseName, OnDropped, ct).ConfigureAwait(false);
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

            // A database name is registered only after the server connection string was resolved.
            if (_connectionString is not { } connectionString)
            {
                return;
            }

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
                        await PostgreSqlTestDatabaseLease.DropDatabase(connectionString, name).ConfigureAwait(false);
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

    // A fixed connection string is rejected at construction, exactly as before the lazy constructor existed.
    private static Func<CancellationToken, Task<string>> CreateFixedResolver(string serverConnectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverConnectionString);
        var connectionString = Normalize(serverConnectionString);
        return _ => Task.FromResult(connectionString);
    }

    private static string Normalize(string serverConnectionString)
    {
        return new NpgsqlConnectionStringBuilder(serverConnectionString) { Database = "postgres" }.ConnectionString;
    }

    private async Task<string> ResolveConnectionString(CancellationToken ct)
    {
        if (_connectionString is { } resolved)
        {
            return resolved;
        }

        var serverConnectionString = await _resolveServerConnectionString(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            throw new InvalidOperationException("The PostgreSQL server connection string resolver returned no connection string.");
        }

        _connectionString = Normalize(serverConnectionString);
        return _connectionString;
    }

    private void OnDropped(string name)
    {
        _ = _databases.TryRemove(name, out _);
    }
}
