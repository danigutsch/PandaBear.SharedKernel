namespace PandaBear.K6.Tool.Tests;

/// <summary>Tests the managed host that a scenario can start before k6 runs.</summary>
public sealed class K6HostTests
{
    [Fact]
    public async Task K6_targets_the_host_after_its_ready_file_and_health_check_and_the_host_stops_after_k6()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await using var server = new HealthServer();
        using var repository = new K6TestRepository(K6HostScenario.Configuration());
        var host = repository.AddFake("app-host", body: K6HostScenario.Ready("http://0.0.0.0:" + K6HostScenario.Port(server)) + "exec /bin/sleep 300");
        var k6 = repository.AddFake("k6", exitCode: 3);
        repository.SetEnvironment("APP_PAYLOAD_BYTES", "1024");

        // Act
        var exitCode = await repository.Invoke(["run", "--scenario", "upload"], TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(3);
        var run = repository.RunDirectories().ShouldHaveSingleItem();
        host.Arguments().ShouldBe(["run", "--urls", "http://127.0.0.1:0"]);
        host.Environment().ShouldContain("APP_READY_FILE=" + Path.Combine(run, "host.url"));
        server.Requests.ShouldContain("GET /health HTTP/1.1");
        k6.Arguments().ShouldContain("APP_UPLOAD_URL=http://127.0.0.1:" + K6HostScenario.Port(server));
        k6.Arguments().ShouldContain("APP_PAYLOAD_BYTES=1024");
        ProcessState.IsRunning(host.ProcessId()).ShouldBeFalse();
        File.Exists(Path.Combine(run, "host.stdout.log")).ShouldBeTrue();
        File.Exists(Path.Combine(run, "host.stderr.log")).ShouldBeTrue();
        repository.Error.ToString().ShouldContain("Host ready: http://127.0.0.1:" + K6HostScenario.Port(server) + Environment.NewLine, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Container_runs_bind_the_host_to_every_address_and_target_it_through_the_host_alias()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await using var server = new HealthServer();
        using var repository = new K6TestRepository(K6HostScenario.Configuration());
        var host = repository.AddFake("app-host", body: K6HostScenario.Ready("http://[::]:" + K6HostScenario.Port(server)) + "exec /bin/sleep 300");
        var podman = repository.AddFake("podman", body: K6TestRepository.WritesContainerSummary);

        // Act
        var exitCode = await repository.Invoke(["run", "--scenario", "upload", "--container"], TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        host.Arguments().ShouldBe(["run", "--urls", "http://0.0.0.0:0"]);
        podman.Arguments().ShouldContain("APP_UPLOAD_URL=http://host.docker.internal:" + K6HostScenario.Port(server));
    }

    [Fact]
    public async Task A_host_that_exits_before_it_is_ready_fails_without_starting_k6()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = new K6TestRepository(K6HostScenario.Configuration());
        repository.AddFake("app-host", exitCode: 4, body: "printf 'boom\\n' >&2");
        var k6 = repository.AddFake("k6");

        // Act
        var exitCode = await repository.Invoke(["run", "--scenario", "upload"], TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(2);
        var run = repository.RunDirectories().ShouldHaveSingleItem();
        var relative = "perf/results/" + Path.GetFileName(run);
        repository.Error.ToString().ShouldContain(
            $"The host of scenario upload exited with code 4 before it was ready. See {relative}/host.stdout.log and {relative}/host.stderr.log.", StringComparison.Ordinal);
        var hostErrors = await File.ReadAllTextAsync(Path.Combine(run, "host.stderr.log"), TestContext.Current.CancellationToken);
        hostErrors.ShouldBe("boom\n");
        k6.Started.ShouldBeFalse();
    }

    [Fact]
    public async Task A_host_that_never_writes_its_ready_file_times_out()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = new K6TestRepository(K6HostScenario.Configuration(readyTimeoutSeconds: 1));
        var host = repository.AddFake("app-host", body: "exec /bin/sleep 300");
        var k6 = repository.AddFake("k6");

        // Act
        var exitCode = await repository.Invoke(["run", "--scenario", "upload"], TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(2);
        repository.Error.ToString().ShouldContain("The host of scenario upload did not write APP_READY_FILE within 1 seconds.", StringComparison.Ordinal);
        k6.Started.ShouldBeFalse();
        ProcessState.IsRunning(host.ProcessId()).ShouldBeFalse();
    }

    [Fact]
    public async Task An_unhealthy_host_times_out()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await using var server = new HealthServer(status: 503);
        using var repository = new K6TestRepository(K6HostScenario.Configuration(healthTimeoutSeconds: 1));
        repository.AddFake("app-host", body: K6HostScenario.Ready("http://127.0.0.1:" + K6HostScenario.Port(server)) + "exec /bin/sleep 300");
        var k6 = repository.AddFake("k6");

        // Act
        var exitCode = await repository.Invoke(["run", "--scenario", "upload"], TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(2);
        repository.Error.ToString().ShouldContain($"The host of scenario upload did not answer http://127.0.0.1:{K6HostScenario.Port(server)}/health with a success status within 1 seconds.", StringComparison.Ordinal);
        k6.Started.ShouldBeFalse();
    }

    [Fact]
    public async Task A_host_that_reports_an_external_address_fails()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = new K6TestRepository(K6HostScenario.Configuration());
        repository.AddFake("app-host", body: K6HostScenario.Ready("https://example.com") + "exec /bin/sleep 300");
        var k6 = repository.AddFake("k6");

        // Act
        var exitCode = await repository.Invoke(["run", "--scenario", "upload"], TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(2);
        repository.Error.ToString().ShouldContain(
            "APP_READY_FILE must target a loopback host such as localhost, 127.0.0.1, or [::1] unless --allow-external-target is passed.", StringComparison.Ordinal);
        k6.Started.ShouldBeFalse();
    }
}
