using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Aspire.Hosting.Testing;
using SharedKernel.Aspire.Hosting.Umami.Testing;
using SharedKernel.IntegrationTesting;

namespace SharedKernel.TestServices.Host;

/// <summary>
/// Owns the services that the test assemblies of a CI slice borrow, one of each: the PostgreSQL server, and Umami with its
/// test website on that server (see <see cref="SharedTestServices"/>). The CI tool starts it with standard input redirected and
/// <see cref="ReadyFile.Variable"/> set, waits for the ready file, runs the tests with the values it holds, one per variable,
/// and then closes standard input. The host stops the services and exits 0 when standard input ends or fails or SIGTERM,
/// SIGINT or SIGHUP arrives; a second signal ends the process at once. A failure to start the PostgreSQL server exits 1 and
/// leaves no ready file; Umami that does not start is reported on standard error and handed over as unavailable. It never
/// prints a value it hands over, which can carry a password.
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
            await using var services = await SharedTestServices.Start(CreateBuilder(), Console.Error, shutdown.Token).ConfigureAwait(false);
            ReadyFile.Publish(readyFile, services.Values);
            var handedOver = string.Join(", ", services.Values.Select(static value => value.Value == UmamiTestWebsite.Unavailable ? value.Variable + " (unavailable)" : value.Variable));
            await Console.Out.WriteLineAsync($"The test services are ready and the ready file hands over {handedOver}.").ConfigureAwait(false);
            await shutdown.Wait().ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"Stopping the test services: {shutdown.Reason}.").ConfigureAwait(false);
            return 0;
        }
        catch (OperationCanceledException) when (shutdown.Token.IsCancellationRequested)
        {
            await Console.Out.WriteLineAsync($"Stopped before the test services were ready: {shutdown.Reason}.").ConfigureAwait(false);
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
