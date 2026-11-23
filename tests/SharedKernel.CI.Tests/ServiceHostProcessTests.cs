using System.Diagnostics;
using System.Runtime.Versioning;

namespace PandaBear.CI.Tests;

/// <summary>
/// Guards how a service host reads a real service process: the order in which a poll observes the exit of the service and reads
/// its ready file. The service is a fake shell script, and the test reads the ready file itself, so it decides when the service's
/// writes and exit happen within a poll.
/// </summary>
[UnsupportedOSPlatform("windows")]
public sealed class ServiceHostProcessTests
{
    private static readonly string[] Variables = ["APP_DATABASE", "APP_ANALYTICS"];

    [PosixFact]
    public async Task A_service_that_writes_its_values_and_exits_while_a_poll_reads_its_ready_file_is_ready_and_its_exit_fails_the_stop()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var directory = new TemporaryDirectory();
        var script = Path.Combine(directory.Path, "service");
        FakeExecutable.WriteScript(
            script,
            "IFS= read -r line\n"
            + "printf '%s\\n' 'APP_DATABASE=secret-one' 'APP_ANALYTICS=secret-two' > \"$APP_READY_FILE.tmp\"\n"
            + "mv \"$APP_READY_FILE.tmp\" \"$APP_READY_FILE\"\n"
            + "exit 5\n");
        Process? service = null;
        var reads = 0;
        var host = ServiceHost.Start(
            "database",
            new ServiceConfiguration("tests/Host/Host.csproj", "APP_READY_FILE", Variables),
            "Release",
            directory.Path,
            directory.Path,
            startInfo =>
            {
                startInfo.FileName = script;
                startInfo.ArgumentList.Clear();
                service = Process.Start(startInfo) ?? throw new InvalidOperationException("The fake service did not start.");
                return service;
            },
            async (path, token) =>
            {
                if (++reads == 1)
                {
                    await service!.StandardInput.WriteLineAsync("write your values and exit".AsMemory(), token);
                    await service.WaitForExitAsync(token);
                    throw new FileNotFoundException("The first read looked before the service wrote its values.", path);
                }

                return await File.ReadAllBytesAsync(path, token);
            });
        Func<Task> stop = host.Stop;

        // Act
        var values = await host.WaitUntilReady(ct);
        var exception = await stop.ShouldThrow<InvalidOperationException>();

        // Assert
        values.ShouldBe(
            [("APP_DATABASE", "secret-one"), ("APP_ANALYTICS", "secret-two")],
            "The service wrote its values before it exited, so an exit that a poll sees after its read found nothing must not hide them.");
        exception.Message.ShouldStartWith("The service of slice database exited with code 5 before it was stopped.", StringComparison.Ordinal);
    }
}
