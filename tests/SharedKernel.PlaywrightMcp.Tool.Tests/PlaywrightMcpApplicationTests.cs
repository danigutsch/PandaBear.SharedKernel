using System.ComponentModel;
using System.Diagnostics;
using PandaBear.Tooling;

namespace PandaBear.PlaywrightMcp.Tool.Tests;

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
        var invocation = 0;
        ProcessStartInfo? runtime = null;

        // Act
        var exitCode = await environment.Invoke(
            ["serve"],
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
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        invocation.ShouldBe(4);
        var runtimeStartInfo = runtime.ShouldNotBeNull();
        runtimeStartInfo.ArgumentList.Take(2).ShouldBe(
            ["--host", "unix:///home/test/.docker/desktop/docker.sock"]);
        runtimeStartInfo.RedirectStandardInput.ShouldBeFalse();
        runtimeStartInfo.RedirectStandardOutput.ShouldBeFalse();
        environment.Output.ToString().ShouldBeEmpty();
        environment.Error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Rejects_a_remote_current_docker_context()
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("docker", PlaywrightMcpTestEnvironment.GetExecutablePath("docker"));

        // Act
        var exitCode = await environment.Invoke(
            ["serve"],
            static (startInfo, _) => Task.FromResult(
                startInfo.ArgumentList.Contains("show")
                    ? new ProcessResult(0, "remote-builder", string.Empty)
                    : new ProcessResult(0, "tcp://remote.example:2375", string.Empty)),
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        environment.Error.ToString().ShouldContain("Refusing non-local Docker context 'remote-builder'", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Returns_the_attached_container_exit_code()
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("docker", PlaywrightMcpTestEnvironment.GetExecutablePath("docker"));
        var invocation = 0;

        // Act
        var exitCode = await environment.Invoke(
            ["serve"],
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
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(17);
        invocation.ShouldBe(5);
        environment.Output.ToString().ShouldBeEmpty();
        environment.Error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Fails_closed_when_docker_context_inspection_fails()
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("docker", PlaywrightMcpTestEnvironment.GetExecutablePath("docker"));
        var invocation = 0;
        ProcessStartInfo? probe = null;

        // Act
        var exitCode = await environment.Invoke(
            ["serve"],
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
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        invocation.ShouldBe(2);
        environment.Error.ToString().ShouldContain("Docker context inspection failed: context denied", StringComparison.Ordinal);
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
        var invocation = 0;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        var exitCode = await environment.Invoke(
            ["serve"],
            (_, _) =>
            {
                invocation++;
                return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
            },
            cancellation.Token);

        // Assert
        exitCode.ShouldBe(1);
        invocation.ShouldBe(0);
        environment.Error.ToString().ShouldContain("Playwright MCP was cancelled", StringComparison.Ordinal);
    }

    [Fact]
    public void Relative_path_entries_cannot_resolve_a_container_runtime()
    {
        // Act
        var executable = ExecutableResolver.Resolve("docker", ".", OperatingSystem.IsWindows());

        // Assert
        executable.ShouldBeNull();
    }

    [Fact]
    public void Files_without_an_execute_bit_cannot_resolve_a_container_runtime()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var notExecutable = new TemporaryDirectory();
        using var executable = new TemporaryDirectory();
        var skippedPath = Path.Combine(notExecutable.Path, "docker");
        File.WriteAllText(skippedPath, string.Empty);
        File.SetUnixFileMode(skippedPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        var expectedPath = Path.Combine(executable.Path, "docker");
        File.WriteAllText(expectedPath, string.Empty);
        File.SetUnixFileMode(expectedPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var path = $"{notExecutable.Path}{Path.PathSeparator}{executable.Path}";

        // Act
        var resolvedPath = ExecutableResolver.Resolve("docker", path, isWindows: false);

        // Assert
        resolvedPath.ShouldBe(expectedPath, "A file without an execute bit cannot start, so the next PATH entry supplies the runtime.");
    }

    [Fact]
    public async Task Rejects_non_rootless_podman_before_runtime_start()
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("podman", PlaywrightMcpTestEnvironment.GetExecutablePath("podman"));
        var invocation = 0;

        // Act
        var exitCode = await environment.Invoke(
            ["serve"],
            (_, _) =>
            {
                invocation++;
                return Task.FromResult(new ProcessResult(0, "false", string.Empty));
            },
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        invocation.ShouldBe(1);
        environment.Error.ToString().ShouldContain("requires local rootless Podman mode", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancellation_force_removes_the_named_container()
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("docker", PlaywrightMcpTestEnvironment.GetExecutablePath("docker"));
        var invocation = 0;
        ProcessStartInfo? cleanup = null;

        // Act
        var exitCode = await environment.Invoke(
            ["serve"],
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
            TestContext.Current.CancellationToken);

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
        var invocation = 0;

        // Act
        var exitCode = await environment.Invoke(
            ["serve"],
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
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        invocation.ShouldBe(5);
        environment.Error.ToString().ShouldContain("Container cleanup failed for 'sharedkernel-playwright-mcp-", StringComparison.Ordinal);
        environment.Error.ToString().ShouldContain("permission denied", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Prepare_returns_pull_exit_code_without_runtime_cleanup()
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("docker", PlaywrightMcpTestEnvironment.GetExecutablePath("docker"));
        var invocation = 0;
        ProcessStartInfo? maintenance = null;

        // Act
        var exitCode = await environment.Invoke(
            ["prepare"],
            (startInfo, _) =>
            {
                invocation++;
                if (invocation == 3)
                {
                    maintenance = startInfo;
                }

                return Task.FromResult(invocation switch
                {
                    1 => new ProcessResult(0, "default", string.Empty),
                    2 => new ProcessResult(0, "unix:///var/run/docker.sock", string.Empty),
                    _ => new ProcessResult(17, string.Empty, string.Empty)
                });
            },
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(17);
        invocation.ShouldBe(3);
        maintenance.ShouldNotBeNull().ArgumentList.ToArray().ShouldBe(
        [
            "--host",
            "unix:///var/run/docker.sock",
            "pull",
            PlaywrightMcpCommand.Image
        ]);
        environment.Error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Clean_returns_image_remove_exit_code_without_runtime_cleanup()
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("podman", PlaywrightMcpTestEnvironment.GetExecutablePath("podman"));
        var invocation = 0;
        ProcessStartInfo? maintenance = null;

        // Act
        var exitCode = await environment.Invoke(
            ["clean"],
            (startInfo, _) =>
            {
                invocation++;
                if (invocation == 2)
                {
                    maintenance = startInfo;
                }

                return Task.FromResult(invocation == 1
                    ? new ProcessResult(0, "true", string.Empty)
                    : new ProcessResult(17, string.Empty, string.Empty));
            },
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(17);
        invocation.ShouldBe(2);
        maintenance.ShouldNotBeNull().ArgumentList.ToArray().ShouldBe(
        [
            "--remote=false",
            "image",
            "rm",
            PlaywrightMcpCommand.Image
        ]);
        environment.Error.ToString().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("prepare")]
    [InlineData("clean")]
    public async Task Prepare_and_clean_write_the_engine_output_to_standard_error(string command)
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("docker", PlaywrightMcpTestEnvironment.GetExecutablePath("docker"));
        var invocation = 0;

        // Act
        var exitCode = await environment.Invoke(
            [command],
            (_, _) =>
            {
                invocation++;
                return Task.FromResult(invocation switch
                {
                    1 => new ProcessResult(0, "default", string.Empty),
                    2 => new ProcessResult(0, "unix:///var/run/docker.sock", string.Empty),
                    _ => new ProcessResult(0, "engine output" + Environment.NewLine, "engine warning" + Environment.NewLine)
                });
            },
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        invocation.ShouldBe(3);
        environment.Output.ToString().ShouldBeEmpty();
        environment.Error.ToString().ShouldBe(
            "engine output" + Environment.NewLine + "engine warning" + Environment.NewLine);
    }

    [Theory]
    [InlineData("docker", "Docker current-context inspection failed: preflight denied")]
    [InlineData("podman", "Podman rootless inspection failed: preflight denied")]
    public async Task Engine_preflight_failures_stop_before_runtime_start(
        string engine,
        string expectedMessage)
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable(engine, PlaywrightMcpTestEnvironment.GetExecutablePath(engine));
        var invocation = 0;

        // Act
        var exitCode = await environment.Invoke(
            ["serve"],
            (_, _) =>
            {
                invocation++;
                return Task.FromResult(new ProcessResult(11, string.Empty, "preflight denied"));
            },
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        invocation.ShouldBe(1);
        environment.Error.ToString().ShouldContain(expectedMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("default\rremote")]
    [InlineData("default\nremote")]
    public async Task Invalid_docker_context_names_are_rejected_before_context_inspection(
        string contextName)
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("docker", PlaywrightMcpTestEnvironment.GetExecutablePath("docker"));
        var invocation = 0;

        // Act
        var exitCode = await environment.Invoke(
            ["serve"],
            (_, _) =>
            {
                invocation++;
                return Task.FromResult(new ProcessResult(0, contextName, string.Empty));
            },
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        invocation.ShouldBe(1);
        environment.Error.ToString().ShouldContain("Docker returned an invalid current context name.", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("invalid", "runtime failed")]
    [InlineData("win32", "Could not start Playwright MCP: runtime failed")]
    [InlineData("io", "Playwright MCP process I/O failed: runtime failed")]
    public async Task Runtime_process_failures_force_cleanup_before_reporting_the_error(
        string failureKind,
        string expectedMessage)
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("docker", PlaywrightMcpTestEnvironment.GetExecutablePath("docker"));
        var invocation = 0;
        ProcessStartInfo? cleanup = null;

        // Act
        var exitCode = await environment.Invoke(
            ["serve"],
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
                    Exception exception = failureKind switch
                    {
                        "invalid" => new InvalidOperationException("runtime failed"),
                        "win32" => new Win32Exception("runtime failed"),
                        _ => new IOException("runtime failed")
                    };
                    return Task.FromException<ProcessResult>(exception);
                }

                cleanup = startInfo;
                return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
            },
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        invocation.ShouldBe(4);
        cleanup.ShouldNotBeNull().ArgumentList.ShouldContain("--force");
        environment.Error.ToString().ShouldContain(expectedMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Podman_cleanup_failures_are_reported_after_runtime_exit()
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("podman", PlaywrightMcpTestEnvironment.GetExecutablePath("podman"));
        var invocation = 0;

        // Act
        var exitCode = await environment.Invoke(
            ["serve"],
            (_, _) =>
            {
                invocation++;
                return Task.FromResult(invocation switch
                {
                    1 => new ProcessResult(0, "true", string.Empty),
                    2 => new ProcessResult(0, string.Empty, string.Empty),
                    _ => new ProcessResult(1, string.Empty, "permission denied")
                });
            },
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        invocation.ShouldBe(3);
        environment.Error.ToString().ShouldContain("Container cleanup failed for 'sharedkernel-playwright-mcp-", StringComparison.Ordinal);
        environment.Error.ToString().ShouldContain("permission denied", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cleanup_timeout_is_reported_with_the_container_name()
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("docker", PlaywrightMcpTestEnvironment.GetExecutablePath("docker"));
        var invocation = 0;

        // Act
        var exitCode = await environment.Invoke(
            ["serve"],
            (_, _) =>
            {
                invocation++;
                return invocation switch
                {
                    1 => Task.FromResult(new ProcessResult(0, "default", string.Empty)),
                    2 => Task.FromResult(new ProcessResult(0, "unix:///var/run/docker.sock", string.Empty)),
                    3 => Task.FromResult(new ProcessResult(0, string.Empty, string.Empty)),
                    _ => Task.FromException<ProcessResult>(new OperationCanceledException())
                };
            },
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        invocation.ShouldBe(4);
        environment.Error.ToString().ShouldContain("Container cleanup timed out for 'sharedkernel-playwright-mcp-", StringComparison.Ordinal);
        environment.Error.ToString().ShouldContain("Remove it manually.", StringComparison.Ordinal);
    }

    [Fact]
    public void Resolves_the_built_tool_from_a_quoted_absolute_path_entry()
    {
        // Arrange
        var command = "SharedKernel.PlaywrightMcp.Tool";
        var path = $"\"{AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)}\"";
        var expectedFileName = OperatingSystem.IsWindows() ? $"{command}.exe" : command;

        // Act
        var executable = ExecutableResolver.Resolve(
            command,
            path,
            OperatingSystem.IsWindows());

        // Assert
        executable.ShouldBe(Path.Combine(AppContext.BaseDirectory, expectedFileName));
    }

    [Fact]
    public async Task Run_process_executes_the_built_tool_and_captures_standard_error()
    {
        // Arrange
        var fileName = OperatingSystem.IsWindows()
            ? "SharedKernel.PlaywrightMcp.Tool.exe"
            : "SharedKernel.PlaywrightMcp.Tool";
        var startInfo = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, fileName))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("serve");
        startInfo.Environment["PATH"] = string.Empty;
        foreach (var variable in new[]
        {
            "PLAYWRIGHT_MCP_ENGINE",
            "PLAYWRIGHT_MCP_NETWORK_ACCESS",
            "PLAYWRIGHT_MCP_IMAGE_RESPONSES",
            "DOCKER_HOST",
            "DOCKER_CONTEXT",
            "CONTAINER_HOST",
            "CONTAINER_CONNECTION"
        })
        {
            startInfo.Environment.Remove(variable);
        }

        // Act
        var result = await PlaywrightMcpApplication.RunProcess(
            startInfo,
            null,
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.StandardOutput.ShouldBeEmpty();
        result.StandardError.ShouldContain("Playwright MCP requires Docker or Podman.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancellation_is_preserved_after_terminating_a_running_child()
    {
        // Arrange
        await using var child = new CancellableChildProcess();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);

        // Act
        var execution = PlaywrightMcpApplication.RunProcess(child.StartInfo, null, cancellation.Token);
        await child.WaitUntilReady(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        Func<Task> waitForExecution = async () => await execution;

        // Assert
        await waitForExecution.ShouldThrowAssignableTo<OperationCanceledException>();
        child.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task Run_process_forwards_each_output_line_while_the_child_runs()
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        var releasePath = Path.Combine(directory.Path, "release");
        var startInfo = new ProcessStartInfo("/bin/sh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(
            "echo pulling; i=0; while [ ! -e \"$1\" ]; do i=$((i + 1)); if [ \"$i\" -gt 300 ]; then exit 3; fi; sleep 0.1; done; echo pulled >&2");
        startInfo.ArgumentList.Add("sh");
        startInfo.ArgumentList.Add(releasePath);
        using var error = new FirstLineWriter();
        var firstLine = error.FirstLine;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));

        // Act
        // The child exits only after the release file exists, which is created only after its first line reached the writer.
        var release = Task.Run(
            async () =>
            {
                var forwarded = await firstLine.WaitAsync(timeout.Token);
                await File.WriteAllTextAsync(releasePath, string.Empty, timeout.Token);
                return forwarded;
            },
            timeout.Token);
        var result = await PlaywrightMcpApplication.RunProcess(startInfo, error, timeout.Token);
        var forwardedWhileRunning = await release;

        // Assert
        forwardedWhileRunning.ShouldBe("pulling" + Environment.NewLine);
        result.ShouldBe(new ProcessResult(0, string.Empty, string.Empty));
        error.ToString().ShouldBe("pulling" + Environment.NewLine + "pulled" + Environment.NewLine);
    }
}
