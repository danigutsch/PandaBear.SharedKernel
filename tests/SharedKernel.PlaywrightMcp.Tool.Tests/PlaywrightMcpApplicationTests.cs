namespace SharedKernel.PlaywrightMcp.Tool.Tests;

[Trait(TestTraitNames.CategoryName, TestTraitValues.SecurityCategory)]
[Trait(TestTraitNames.ScopeName, SharedKernelTestTraitNames.UnitScope)]
public sealed class PlaywrightMcpApplicationTests
{
    [Fact]
    public async Task Uses_the_current_local_docker_context_for_runtime()
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("docker", PlaywrightMcpTestEnvironment.GetExecutablePath("docker"));
        var error = new StringWriter();
        var invocation = 0;
        System.Diagnostics.ProcessStartInfo? runtime = null;

        // Act
        var exitCode = await PlaywrightMcpApplication.Run(
            [],
            error,
            environment.GetEnvironmentVariable,
            environment.ResolveExecutable,
            (startInfo, _) =>
            {
                invocation++;
                if (invocation == 1)
                {
                    return Task.FromResult(new ProcessResult(0, "desktop-linux", string.Empty));
                }

                if (invocation == 2)
                {
                    return Task.FromResult(new ProcessResult(0, "unix:///home/test/.docker/desktop/docker.sock", string.Empty));
                }

                if (invocation == 3)
                {
                    runtime = startInfo;
                }

                return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
            },
            CancellationToken.None);

        // Assert
        exitCode.ShouldBe(0);
        invocation.ShouldBe(4);
        runtime.ShouldNotBeNull().ArgumentList.Take(2).ShouldBe(
            ["--host", "unix:///home/test/.docker/desktop/docker.sock"]);
        error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Rejects_a_remote_current_docker_context()
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("docker", PlaywrightMcpTestEnvironment.GetExecutablePath("docker"));
        var error = new StringWriter();

        // Act
        var exitCode = await PlaywrightMcpApplication.Run(
            [],
            error,
            environment.GetEnvironmentVariable,
            environment.ResolveExecutable,
            static (startInfo, _) => Task.FromResult(
                startInfo.ArgumentList.Contains("show")
                    ? new ProcessResult(0, "remote-builder", string.Empty)
                    : new ProcessResult(0, "tcp://remote.example:2375", string.Empty)),
            CancellationToken.None);

        // Assert
        exitCode.ShouldBe(1);
        error.ToString().ShouldContain("Refusing non-local Docker context 'remote-builder'", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Returns_the_attached_container_exit_code()
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("docker", PlaywrightMcpTestEnvironment.GetExecutablePath("docker"));
        var error = new StringWriter();
        var invocation = 0;

        // Act
        var exitCode = await PlaywrightMcpApplication.Run(
            [],
            error,
            environment.GetEnvironmentVariable,
            environment.ResolveExecutable,
            (_, _) =>
            {
                invocation++;
                return Task.FromResult(invocation switch
                {
                    1 => new ProcessResult(0, "default", string.Empty),
                    2 => new ProcessResult(0, "unix:///var/run/docker.sock", string.Empty),
                    3 => new ProcessResult(17, string.Empty, string.Empty),
                    4 => new ProcessResult(1, string.Empty, "No such container"),
                    _ => new ProcessResult(0, string.Empty, string.Empty)
                });
            },
            CancellationToken.None);

        // Assert
        exitCode.ShouldBe(17);
        invocation.ShouldBe(5);
        error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Fails_closed_when_docker_context_inspection_fails()
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("docker", PlaywrightMcpTestEnvironment.GetExecutablePath("docker"));
        var error = new StringWriter();
        var invocation = 0;
        System.Diagnostics.ProcessStartInfo? probe = null;

        // Act
        var exitCode = await PlaywrightMcpApplication.Run(
            [],
            error,
            environment.GetEnvironmentVariable,
            environment.ResolveExecutable,
            (startInfo, _) =>
            {
                invocation++;
                if (invocation == 1)
                {
                    return Task.FromResult(new ProcessResult(0, "default", string.Empty));
                }

                probe = startInfo;
                return Task.FromResult(new ProcessResult(1, "unix:///var/run/docker.sock", "context denied"));
            },
            CancellationToken.None);

        // Assert
        exitCode.ShouldBe(1);
        invocation.ShouldBe(2);
        error.ToString().ShouldContain("Docker context inspection failed: context denied", StringComparison.Ordinal);
        var probeStartInfo = probe.ShouldNotBeNull();
        probeStartInfo.ArgumentList.ToArray().ShouldBe(
        [
            "--context",
            "default",
            "context",
            "inspect",
            "default",
            "--format",
            "{{.Endpoints.docker.Host}}"
        ]);
    }

    [Fact]
    public async Task Pre_cancelled_execution_does_not_start_a_process()
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("docker", PlaywrightMcpTestEnvironment.GetExecutablePath("docker"));
        var error = new StringWriter();
        var invocation = 0;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        var exitCode = await PlaywrightMcpApplication.Run(
            [],
            error,
            environment.GetEnvironmentVariable,
            environment.ResolveExecutable,
            (_, _) =>
            {
                invocation++;
                return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
            },
            cancellation.Token);

        // Assert
        exitCode.ShouldBe(1);
        invocation.ShouldBe(0);
        error.ToString().ShouldContain("Playwright MCP was cancelled", StringComparison.Ordinal);
    }

    [Fact]
    public void Relative_path_entries_cannot_resolve_a_container_runtime()
    {
        // Act
        var executable = PlaywrightMcpApplication.ResolveExecutable("docker", ".", OperatingSystem.IsWindows());

        // Assert
        executable.ShouldBeNull();
    }

    [Fact]
    public async Task Rejects_non_rootless_podman_before_runtime_start()
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("podman", PlaywrightMcpTestEnvironment.GetExecutablePath("podman"));
        var error = new StringWriter();
        var invocation = 0;

        // Act
        var exitCode = await PlaywrightMcpApplication.Run(
            [],
            error,
            environment.GetEnvironmentVariable,
            environment.ResolveExecutable,
            (_, _) =>
            {
                invocation++;
                return Task.FromResult(new ProcessResult(0, "false", string.Empty));
            },
            CancellationToken.None);

        // Assert
        exitCode.ShouldBe(1);
        invocation.ShouldBe(1);
        error.ToString().ShouldContain("requires local rootless Podman mode", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancellation_force_removes_the_named_container()
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("docker", PlaywrightMcpTestEnvironment.GetExecutablePath("docker"));
        var error = new StringWriter();
        var invocation = 0;
        System.Diagnostics.ProcessStartInfo? cleanup = null;

        // Act
        var exitCode = await PlaywrightMcpApplication.Run(
            [],
            error,
            environment.GetEnvironmentVariable,
            environment.ResolveExecutable,
            (startInfo, _) =>
            {
                invocation++;
                if (invocation == 1)
                {
                    return Task.FromResult(new ProcessResult(0, "default", string.Empty));
                }

                if (invocation == 2)
                {
                    return Task.FromResult(new ProcessResult(0, "unix:///var/run/docker.sock", string.Empty));
                }

                if (invocation == 3)
                {
                    return Task.FromException<ProcessResult>(new OperationCanceledException());
                }

                cleanup = startInfo;
                return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
            },
            CancellationToken.None);

        // Assert
        exitCode.ShouldBe(1);
        invocation.ShouldBe(4);
        var cleanupStartInfo = cleanup.ShouldNotBeNull();
        cleanupStartInfo.ArgumentList.ShouldContain("rm");
        cleanupStartInfo.ArgumentList.ShouldContain("--force");
        cleanupStartInfo.ArgumentList.ShouldContain(static argument => argument.StartsWith("sharedkernel-playwright-mcp-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Reports_failed_cancellation_cleanup_with_the_container_name()
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("docker", PlaywrightMcpTestEnvironment.GetExecutablePath("docker"));
        var error = new StringWriter();
        var invocation = 0;

        // Act
        var exitCode = await PlaywrightMcpApplication.Run(
            [],
            error,
            environment.GetEnvironmentVariable,
            environment.ResolveExecutable,
            (_, _) =>
            {
                invocation++;
                return invocation switch
                {
                    1 => Task.FromResult(new ProcessResult(0, "default", string.Empty)),
                    2 => Task.FromResult(new ProcessResult(0, "unix:///var/run/docker.sock", string.Empty)),
                    3 => Task.FromException<ProcessResult>(new OperationCanceledException()),
                    4 => Task.FromResult(new ProcessResult(1, string.Empty, "permission denied")),
                    _ => Task.FromResult(new ProcessResult(0, "container-id", string.Empty))
                };
            },
            CancellationToken.None);

        // Assert
        exitCode.ShouldBe(1);
        invocation.ShouldBe(5);
        error.ToString().ShouldContain("Container cleanup failed for 'sharedkernel-playwright-mcp-", StringComparison.Ordinal);
        error.ToString().ShouldContain("permission denied", StringComparison.Ordinal);
    }
}
