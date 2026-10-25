using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Aspire.Hosting.Testing;
using PandaBear.IntegrationTesting;

namespace PandaBear.Npgsql.Testing.Host;

/// <summary>
/// Owns the one PostgreSQL server that the database test assemblies of a CI slice borrow. The CI tool starts it with
/// standard input redirected and <see cref="PostgreSqlTestServer.ReadyFileVariable"/> set, waits for the ready file,
/// runs the tests with the connection string it holds, and then closes standard input. The host stops the server and
/// exits 0 when standard input ends or fails or SIGTERM, SIGINT or SIGHUP arrives; a second signal ends the process at
/// once, and a failure to start the server exits 1 and leaves no ready file. It never prints the connection string, which
/// carries the password.
/// </summary>
internal static class Program
{
    // ponytail: Aspire's container runtime check does not observe the stop token, so a stop that arrives while it retries
    // (about a minute without a runtime) is honored only afterwards, and the host then exits 1, not 0; a second signal
    // ends the process at once. Aspire's own startup timeout cancels the start with no stop requested, so it reaches the
    // generic catch below as a cancellation; name the timeout there if that message ever hides a cause.
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The host reports every failure on standard error and exits 1; an unhandled exception would only end the process without stopping the container first.")]
    private static async Task<int> Main()
    {
        using var standardInput = Console.OpenStandardInput();
        using var shutdown = new ShutdownRequest(standardInput, ShutdownRequest.TerminationSignals);
        try
        {
            var readyFile = ReadyFile.Resolve(Environment.GetEnvironmentVariable);
            await using var server = await PostgreSqlTestServer.Start(CreateBuilder(), shutdown.Token).ConfigureAwait(false);
            ReadyFile.Publish(readyFile, server.ConnectionString);
            await Console.Out.WriteLineAsync("The PostgreSQL test server accepts connections and the ready file is written.").ConfigureAwait(false);
            await shutdown.Wait().ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"Stopping the PostgreSQL test server: {shutdown.Reason}.").ConfigureAwait(false);
            return 0;
        }
        catch (OperationCanceledException) when (shutdown.Token.IsCancellationRequested)
        {
            await Console.Out.WriteLineAsync($"Stopped before the PostgreSQL test server was ready: {shutdown.Reason}.").ConfigureAwait(false);
            return 0;
        }
        catch (Exception failure)
        {
            await Console.Error.WriteLineAsync(failure.ToString()).ConfigureAwait(false);
            return 1;
        }
    }

    // Aspire locates generated DCP metadata by walking the synchronous builder-creation stack, so the builder is created
    // here, in an assembly that uses the Aspire AppHost SDK, before the first await.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IDistributedApplicationTestingBuilder CreateBuilder() => AspireTestApplication.CreateBuilder();
}
