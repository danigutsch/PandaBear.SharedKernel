using Aspire.Hosting.Testing;
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
    internal PostgreSqlTestServerSource(Func<CancellationToken, Task<StartedPostgreSqlTestServer>> start, Func<string, string?>? readEnvironment = null)
    {
        ArgumentNullException.ThrowIfNull(start);
        _owned = new OnDemandResource<StartedPostgreSqlTestServer>(start);
        _readEnvironment = readEnvironment ?? Environment.GetEnvironmentVariable;
    }

    /// <summary>Gets the connection string of the server, starting one first when no owner supplied one.</summary>
    /// <param name="ct">Abandons only this caller's wait; it never cancels the shared start.</param>
    /// <returns>The server connection string. A failed start is cached and reaches every later caller.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the borrowed-server variable is set but malformed.</exception>
    internal async Task<string> GetConnectionString(CancellationToken ct)
    {
        if (PostgreSqlTestServer.Borrowed(_readEnvironment) is { } borrowed)
        {
            return borrowed;
        }

        var server = await _owned.Get(ct).ConfigureAwait(false);
        return server.ConnectionString;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _owned.DisposeAsync();

    // The resource runs this delegate synchronously on the first caller's thread, so the consuming assembly's
    // CreateBuilder is still on the stack when it creates the Aspire builder.
    private static Func<CancellationToken, Task<StartedPostgreSqlTestServer>> StartOn(Func<IDistributedApplicationTestingBuilder> createBuilder)
    {
        ArgumentNullException.ThrowIfNull(createBuilder);
        return ct => PostgreSqlTestServer.Start(createBuilder(), ct);
    }
}
