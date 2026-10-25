using Aspire.Hosting.Testing;
using Npgsql;
using PandaBear.IntegrationTesting;

namespace PandaBear.Npgsql.Testing;

/// <summary>
/// Resolves the PostgreSQL server that an assembly's tests use: the server another process owns when
/// <see cref="PostgreSqlTestServer.ConnectionStringVariable"/> is set, otherwise a server this source starts on its
/// first demand. Disposing the source stops only a server that it started.
/// </summary>
internal sealed class PostgreSqlTestServerSource : IAsyncDisposable
{
    private readonly Func<string, string?> _readEnvironment;
    private readonly Func<string, CancellationToken, Task> _reach;
    private readonly OnDemandResource<string?> _borrowed;
    private readonly OnDemandResource<StartedPostgreSqlTestServer> _owned;

    /// <summary>Creates a source that starts its own server on a testing builder from <paramref name="createBuilder"/>.</summary>
    /// <param name="createBuilder">
    /// Creates the testing builder. It must be an override in an assembly that uses the Aspire AppHost SDK, marked
    /// <see cref="System.Runtime.CompilerServices.MethodImplOptions.NoInlining"/>, because the first demand calls it on
    /// the demanding thread and Aspire finds its DCP metadata by walking that call stack.
    /// </param>
    /// <param name="readEnvironment">
    /// Reads an environment variable; the process environment when it is <see langword="null"/>. Tests supply their own
    /// so that they never change a process-wide variable.
    /// </param>
    internal PostgreSqlTestServerSource(Func<IDistributedApplicationTestingBuilder> createBuilder, Func<string, string?>? readEnvironment = null)
        : this(StartOn(createBuilder), readEnvironment)
    {
    }

    /// <summary>Creates a source that starts its own server with <paramref name="start"/>.</summary>
    /// <param name="start">Starts the server. It receives a token that disposal cancels while the start is pending.</param>
    /// <param name="readEnvironment">
    /// Reads an environment variable; the process environment when it is <see langword="null"/>.
    /// </param>
    /// <param name="reach">
    /// Connects once to a borrowed server and throws when it does not accept the connection; <see cref="PostgreSqlTestServer.Reach"/>
    /// when it is <see langword="null"/>. Tests supply their own so that a borrowed server needs no network.
    /// </param>
    internal PostgreSqlTestServerSource(
        Func<CancellationToken, Task<StartedPostgreSqlTestServer>> start,
        Func<string, string?>? readEnvironment = null,
        Func<string, CancellationToken, Task>? reach = null)
    {
        ArgumentNullException.ThrowIfNull(start);
        _owned = new OnDemandResource<StartedPostgreSqlTestServer>(start);
        _borrowed = new OnDemandResource<string?>(Borrow);
        _readEnvironment = readEnvironment ?? Environment.GetEnvironmentVariable;
        _reach = reach ?? PostgreSqlTestServer.Reach;
    }

    /// <summary>Gets the connection string of the server, starting one first when no owner supplied one.</summary>
    /// <param name="ct">Abandons only this caller's wait; it never cancels the shared start or the shared first connection.</param>
    /// <returns>
    /// The server connection string. A failed start, and a borrowed server that does not accept a connection, are cached:
    /// the failure reaches every later caller without another wait.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the borrowed-server variable is set but malformed.</exception>
    /// <exception cref="NpgsqlException">Thrown when the borrowed server does not accept a connection.</exception>
    internal async Task<string> GetConnectionString(CancellationToken ct)
    {
        if (await _borrowed.Get(ct).ConfigureAwait(false) is { } borrowed)
        {
            return borrowed;
        }

        var server = await _owned.Get(ct).ConfigureAwait(false);
        return server.ConnectionString;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // A set variable never starts a server of the source's own, so at most one of the two has a failure to report.
        try
        {
            await _borrowed.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            await _owned.DisposeAsync().ConfigureAwait(false);
        }
    }

    // The first demand reads the variable and, when it names a server, connects to it once. Every database request waits for
    // the server inside the pool's gate, so a server that accepts connections and never answers would otherwise cost one
    // connection timeout, twice, per test; the cached outcome costs the assembly one.
    private async Task<string?> Borrow(CancellationToken ct)
    {
        if (PostgreSqlTestServer.Borrowed(_readEnvironment) is not { } borrowed)
        {
            return null;
        }

        await _reach(borrowed, ct).ConfigureAwait(false);
        return borrowed;
    }

    // The resource runs this delegate synchronously on the first caller's thread, so the consuming assembly's
    // CreateBuilder is still on the stack when it creates the Aspire builder.
    private static Func<CancellationToken, Task<StartedPostgreSqlTestServer>> StartOn(Func<IDistributedApplicationTestingBuilder> createBuilder)
    {
        ArgumentNullException.ThrowIfNull(createBuilder);
        return ct => PostgreSqlTestServer.Start(createBuilder(), ct);
    }
}
