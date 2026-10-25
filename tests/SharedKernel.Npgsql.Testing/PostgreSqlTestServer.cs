using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Npgsql;
using PandaBear.IntegrationTesting;

namespace PandaBear.Npgsql.Testing;

/// <summary>
/// Knows how the PostgreSQL test server that several test assemblies share is composed, borrowed from the process that
/// owns it, and started. The assembly fixtures and the owner host call it, so they cannot drift apart.
/// </summary>
internal static class PostgreSqlTestServer
{
    /// <summary>Names the environment variable that carries the connection string of a server another process owns.</summary>
    internal const string ConnectionStringVariable = "SHAREDKERNEL_TESTING_POSTGRESQL_CONNECTION_STRING";

    /// <summary>Names the environment variable that tells the owner host where to write its ready file.</summary>
    internal const string ReadyFileVariable = "SHAREDKERNEL_TESTING_POSTGRESQL_READY_FILE";

    private const string ResourceName = "postgres";

    /// <summary>Reads the connection string of a server that another process owns.</summary>
    /// <param name="readEnvironment">Reads an environment variable; returns <see langword="null"/> when it is not set.</param>
    /// <returns>
    /// The connection string without surrounding whitespace, or <see langword="null"/> when the variable is not set or
    /// blank, which means that the caller starts a server of its own on demand.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the variable is set but is not a PostgreSQL connection string with a host. A set variable always
    /// borrows or fails; it never falls back to an owned server, which would hide a broken owner and start a second
    /// container. The message names the variable and never repeats its value, which carries the owner's password.
    /// </exception>
    internal static string? Borrowed(Func<string, string?> readEnvironment)
    {
        var value = readEnvironment(ConnectionStringVariable)?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return HasHost(value)
            ? value
            : throw new InvalidOperationException($"The environment variable {ConnectionStringVariable} does not contain a PostgreSQL connection string with a host.");
    }

    /// <summary>Composes the server on <paramref name="builder"/>, starts it and waits until it is healthy.</summary>
    /// <param name="builder">
    /// A testing builder that was created from an assembly that uses the Aspire AppHost SDK, on the synchronous call
    /// path of this call, because Aspire finds its DCP metadata by walking the call stack.
    /// </param>
    /// <param name="ct">Cancels the start; a timeout of its own bounds the startup.</param>
    /// <returns>The started server. Disposing it stops the container.</returns>
    internal static async Task<StartedPostgreSqlTestServer> Start(IDistributedApplicationTestingBuilder builder, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _ = builder.AddPostgres(ResourceName);

        var app = await AspireTestApplication.Start(builder, [ResourceName], resourceStartupTimeout: null, ct).ConfigureAwait(false);
        try
        {
            var connectionString = await app.GetConnectionString(ResourceName, ct).ConfigureAwait(false);
            return new StartedPostgreSqlTestServer(connectionString, app.DisposeAsync);
        }
        catch (Exception initializationFailure)
        {
            await PostgreSqlTestCleanup.Run(initializationFailure, app.DisposeAsync).ConfigureAwait(false);
            throw;
        }
    }

    private static bool HasHost(string connectionString)
    {
        try
        {
            return !string.IsNullOrEmpty(new NpgsqlConnectionStringBuilder(connectionString).Host);
        }
        catch (ArgumentException)
        {
            // The parser's message may repeat a part of the value, so it is dropped rather than wrapped.
            return false;
        }
    }
}
