using System.Runtime.Versioning;

namespace PandaBear.Repository.Tests;

/// <summary>
/// Guards what only a real tool process shows: how <c>run</c> ends a service when a signal cancels it, with the invocation
/// settings of <c>Program.cs</c>, and which environment the service inherits from the process. The service is a fake shell
/// script behind a <c>dotnet</c> shell script.
/// </summary>
[UnsupportedOSPlatform("windows")]
public sealed class SliceServiceProcessTests
{
    private const string Database = ServiceRunScope.DatabaseHost;

    // The stop takes longer than the two seconds that System.CommandLine allows a cancelled invocation by default, as the
    // real host's does while its Aspire application removes the container.
    private static readonly string SlowToStop = FakeServiceHost.Ready(ServiceRunScope.Value)
        + FakeServiceHost.WaitForEndOfInput
        + "sleep 4\n: > \"$FAKE_MARKERS/service.stopped\"\n";

    [PosixFact]
    public async Task A_signal_gives_the_service_the_time_it_needs_to_stop_and_the_run_still_writes_its_evidence()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ServiceToolProcess(SlowToStop, "exec sleep 120\n");
        using var tool = scope.Start();
        await scope.WaitForMarker("test.started", tool, ct);

        // Act
        await tool.Terminate(ct);
        var result = await tool.Exited(ct);

        // Assert
        scope.HasMarker("service.stopped").ShouldBeTrue("The tool waits for the service to finish its stop instead of exiting two seconds after the signal.");
        result.ExitCode.ShouldBe(1, "A cancellation whose cleanup finished exits like any other cancellation, not with 143.");
        scope.ServiceDirectories.ShouldBeEmpty("The ready file's directory is deleted whatever ended the run.");
        (await ServiceRunScope.Phases(scope.Execution, ct)).ShouldBe(
            ["load passed", "restore passed", "build passed", "service-database passed", "test failed", "service-stop-database passed", "total failed"],
            "The evidence of a cancelled run is written, so the job can upload it.");
    }

    [PosixFact]
    public async Task Tokens_in_the_environment_of_the_tool_never_reach_the_service()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ServiceToolProcess(FakeServiceHost.Ready(ServiceRunScope.Value) + FakeServiceHost.WaitForEndOfInput, "exit 0\n");
        using var tool = scope.Start(new Dictionary<string, string?>
        {
            ["GH_TOKEN"] = "token-canary",
            ["GITHUB_TOKEN"] = "token-canary",
            ["github_token"] = "token-canary",
            ["SHAREDKERNEL_UNRELATED"] = "kept",
        });

        // Act
        var result = await tool.Exited(ct);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        var service = scope.Run.Host.Environment(Database);
        service.Keys.ShouldNotContain(
            static name => name.Equals("GH_TOKEN", StringComparison.OrdinalIgnoreCase) || name.Equals("GITHUB_TOKEN", StringComparison.OrdinalIgnoreCase),
            "A service starts containers and downloads images; it needs no token of the job.");
        service["SHAREDKERNEL_UNRELATED"].ShouldBe("kept", "Only the tokens are removed from the inherited environment.");
    }
}
