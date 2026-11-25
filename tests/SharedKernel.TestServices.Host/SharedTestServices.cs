using Aspire.Hosting.Testing;
using SharedKernel.Aspire.Hosting.Umami.Testing;
using SharedKernel.IntegrationTesting;
using SharedKernel.Npgsql.Testing;

namespace SharedKernel.TestServices.Host;

/// <summary>
/// Composes and starts every service that the test assemblies of this repository share, once: the PostgreSQL server, and Umami with its
/// test website, which keeps its data in a database of its own on that server. Each service is composed by the helper that its borrowing
/// fixtures use, so the host and the fixtures cannot drift apart.
/// </summary>
internal static class SharedTestServices
{
    /// <summary>Gets the environment variables that the host hands over, in the order of its ready file and of the CI configuration.</summary>
    internal static IReadOnlyList<string> Variables { get; } = [PostgreSqlTestServer.ConnectionStringVariable, UmamiTestWebsite.ConnectionStringVariable];

    /// <summary>Adds every shared service to <paramref name="builder"/>.</summary>
    /// <param name="builder">The builder.</param>
    internal static void Add(IDistributedApplicationBuilder builder)
    {
        var postgres = PostgreSqlTestServer.Add(builder);
        _ = UmamiTestWebsite.Add(builder, postgres);
    }

    /// <summary>
    /// Starts the services and waits for them. The PostgreSQL server must start, or the start fails; Umami waits for its database on that
    /// server, and when it does not start, <paramref name="error"/> says why and its variable carries <see cref="UmamiTestWebsite.Unavailable"/>,
    /// so the tests that borrow PostgreSQL still run and only the tests that borrow Umami fail.
    /// </summary>
    /// <param name="builder">
    /// A testing builder that was created from an assembly that uses the Aspire AppHost SDK, on the synchronous call path of this call,
    /// because Aspire finds its DCP metadata by walking the call stack.
    /// </param>
    /// <param name="error">Receives the report when Umami does not start.</param>
    /// <param name="ct">Cancels the start.</param>
    /// <returns>The started services. Disposing them stops every container.</returns>
    internal static async Task<StartedTestServices> Start(IDistributedApplicationTestingBuilder builder, TextWriter error, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(builder);
        Add(builder);
        var app = await AspireTestApplication.Start(builder, [PostgreSqlTestServer.ResourceName], resourceStartupTimeout: null, ct).ConfigureAwait(false);
        try
        {
            var postgres = await app.GetConnectionString(PostgreSqlTestServer.ResourceName, ct).ConfigureAwait(false);
            var umami = await UmamiTestWebsite.HandOver(app, error, ct).ConfigureAwait(false);
            return new StartedTestServices([(PostgreSqlTestServer.ConnectionStringVariable, postgres), (UmamiTestWebsite.ConnectionStringVariable, umami)], app.DisposeAsync);
        }
        catch (Exception startFailure)
        {
            try
            {
                await app.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException("The test services did not start, and stopping them failed too.", startFailure, cleanupFailure);
            }

            throw;
        }
    }
}
