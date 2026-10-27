namespace PandaBear.RepoConfig.Tests;

/// <summary>
/// Tests the verify-devcontainer command against a fake devcontainer CLI and fake docker executable.
/// </summary>
public sealed class VerifyDevcontainerCommandTests
{
    [Fact]
    public async Task Up_runs_with_trace_logging_and_tees_its_log_to_output()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new DevcontainerTestWorkspace();

        // Act
        var (exitCode, output, error) = await workspace.Run(["--devcontainer-cli", workspace.CliPath]);

        // Assert
        exitCode.ShouldBe(0, error);
        workspace.CliCalls().ShouldBe([$"[up][--workspace-folder][{workspace.RootPath}][--log-level][trace]"]);
        output.ShouldContain("{\"outcome\":\"success\",\"containerId\":\"abc123\"}", StringComparison.Ordinal);
        output.ShouldContain("trace on stderr", StringComparison.Ordinal);
        var log = await File.ReadAllTextAsync(
            Path.Combine(workspace.RootPath, "TestResults", "devcontainer-smoke", "devcontainer-up.log"),
            TestContext.Current.CancellationToken);
        log.ShouldContain("{\"outcome\":\"success\",\"containerId\":\"abc123\"}", StringComparison.Ordinal);
        log.ShouldContain("trace on stderr", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Outcome_error_fails_even_when_the_cli_exits_zero()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new DevcontainerTestWorkspace();
        workspace.WriteCli(DevcontainerTestWorkspace.UpScript("{\"outcome\":\"error\",\"containerId\":\"abc123\"}"));

        // Act
        var (exitCode, _, error) = await workspace.Run(["--devcontainer-cli", workspace.CliPath, "--exec", "true"]);

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldContain("devcontainer up failed.", StringComparison.Ordinal);
        workspace.CliCalls().Length.ShouldBe(1);
        workspace.DockerCalls().ShouldBe(["[rm][-f][abc123]"]);
    }

    [Fact]
    public async Task Nonzero_up_exit_fails_and_removes_the_reported_container()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new DevcontainerTestWorkspace();
        workspace.WriteCli(DevcontainerTestWorkspace.UpScript("{\"outcome\":\"success\",\"containerId\":\"abc123\"}", exitCode: 7));

        // Act
        var (exitCode, _, error) = await workspace.Run(["--devcontainer-cli", workspace.CliPath]);

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldContain("devcontainer up failed.", StringComparison.Ordinal);
        workspace.DockerCalls().ShouldBe(["[rm][-f][abc123]"]);
    }

    [Fact]
    public async Task Cleanup_removes_the_last_container_id_in_the_trace_log()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new DevcontainerTestWorkspace();
        workspace.WriteCli(
            "echo '[trace] {\"containerId\":\"first\"}' >&2; printf '%s\\n' '{\"outcome\":\"success\",\"containerId\":\"second\"}'; exit 0");

        // Act
        var (exitCode, _, error) = await workspace.Run(["--devcontainer-cli", workspace.CliPath]);

        // Assert
        exitCode.ShouldBe(0, error);
        workspace.DockerCalls().ShouldBe(["[rm][-f][second]"]);
    }

    [Fact]
    public async Task Keep_container_skips_removal()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new DevcontainerTestWorkspace();

        // Act
        var (exitCode, output, error) = await workspace.Run(["--devcontainer-cli", workspace.CliPath, "--keep-container"]);

        // Assert
        exitCode.ShouldBe(0, error);
        output.ShouldContain("Keeping devcontainer 'abc123' because --keep-container is set.", StringComparison.Ordinal);
        workspace.DockerCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Missing_container_id_fails_without_metadata_or_cleanup()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new DevcontainerTestWorkspace();
        workspace.WriteCli(DevcontainerTestWorkspace.UpScript("{\"outcome\":\"success\"}"));

        // Act
        var (exitCode, _, error) = await workspace.Run(["--devcontainer-cli", workspace.CliPath, "--exec", "true"]);

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldContain("Failed to determine the devcontainer container ID", StringComparison.Ordinal);
        workspace.CliCalls().Length.ShouldBe(1);
        workspace.DockerCalls().ShouldBeEmpty();
        File.Exists(Path.Combine(workspace.RootPath, "TestResults", "devcontainer-smoke", "metadata.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task Metadata_is_written_to_the_log_directory()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new DevcontainerTestWorkspace();
        var logDirectory = Path.Combine(workspace.RootPath, "logs", "smoke");

        // Act
        var (exitCode, _, error) = await workspace.Run(
            ["--devcontainer-cli", workspace.CliPath, "--log-directory", "logs/smoke"]);

        // Assert
        exitCode.ShouldBe(0, error);
        var metadata = await File.ReadAllLinesAsync(Path.Combine(logDirectory, "metadata.txt"), TestContext.Current.CancellationToken);
        metadata.ShouldBe(
        [
            $"workspace_folder={workspace.RootPath}",
            $"log_dir={logDirectory}",
            $"devcontainer_cli={workspace.CliPath}",
            "container_id=abc123",
        ]);
        File.Exists(Path.Combine(logDirectory, "devcontainer-up.log")).ShouldBeTrue();
    }

    [Fact]
    public async Task Exec_commands_run_in_order_in_a_login_shell_with_their_own_logs()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new DevcontainerTestWorkspace();

        // Act
        var (exitCode, output, error) = await workspace.Run(
            ["--devcontainer-cli", workspace.CliPath, "--exec", "dotnet --version", "--exec", "git --version"]);

        // Assert
        exitCode.ShouldBe(0, error);
        workspace.CliCalls().Skip(1).ShouldBe(
        [
            $"[exec][--workspace-folder][{workspace.RootPath}][bash][-lc][dotnet --version]",
            $"[exec][--workspace-folder][{workspace.RootPath}][bash][-lc][git --version]",
        ]);
        output.ShouldContain("exec output", StringComparison.Ordinal);
        var logDirectory = Path.Combine(workspace.RootPath, "TestResults", "devcontainer-smoke");
        var execLog = await File.ReadAllTextAsync(Path.Combine(logDirectory, "devcontainer-exec-1.log"), TestContext.Current.CancellationToken);
        execLog.ShouldContain("exec output", StringComparison.Ordinal);
        File.Exists(Path.Combine(logDirectory, "devcontainer-exec-2.log")).ShouldBeTrue();
        workspace.DockerCalls().ShouldBe(["[rm][-f][abc123]"]);
    }

    [Fact]
    public async Task Failing_exec_stops_the_run_and_removes_the_container()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new DevcontainerTestWorkspace();
        workspace.WriteCli(
            DevcontainerTestWorkspace.UpScript("{\"outcome\":\"success\",\"containerId\":\"abc123\"}"),
            exec: "exit 3");

        // Act
        var (exitCode, _, error) = await workspace.Run(
            ["--devcontainer-cli", workspace.CliPath, "--exec", "first", "--exec", "second"]);

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldContain("devcontainer exec 1 failed with exit code 3.", StringComparison.Ordinal);
        workspace.CliCalls().Length.ShouldBe(2);
        workspace.DockerCalls().ShouldBe(["[rm][-f][abc123]"]);
    }

    [Fact]
    public async Task Git_directory_is_not_mounted_for_a_main_worktree()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        using var workspace = new DevcontainerTestWorkspace(repository.RootPath);

        // Act
        var (exitCode, _, error) = await workspace.Run(["--devcontainer-cli", workspace.CliPath]);

        // Assert
        exitCode.ShouldBe(0, error);
        workspace.CliCalls().ShouldBe([$"[up][--workspace-folder][{repository.RootPath}][--log-level][trace]"]);
    }

    [Fact]
    public async Task Git_common_directory_is_mounted_for_a_linked_worktree()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        var commit = await repository.RunGit(
            ["-c", "user.name=Devcontainer Tests", "-c", "user.email=devcontainer-tests@example.invalid", "-c", "commit.gpgsign=false",
                "commit", "--allow-empty", "--quiet", "-m", "base"],
            TestContext.Current.CancellationToken);
        commit.ExitCode.ShouldBe(0);
        var linkedRoot = await repository.CreateLinkedWorktree(TestContext.Current.CancellationToken);
        using var workspace = new DevcontainerTestWorkspace(linkedRoot);
        var commonDirectory = Path.Combine(repository.RootPath, ".git");

        // Act
        var (exitCode, _, error) = await workspace.Run(["--devcontainer-cli", workspace.CliPath]);

        // Assert
        exitCode.ShouldBe(0, error);
        workspace.CliCalls().ShouldBe(
        [
            $"[up][--workspace-folder][{linkedRoot}][--log-level][trace][--mount][type=bind,source={commonDirectory},target={commonDirectory}]",
        ]);
    }

    [Fact]
    public async Task Missing_devcontainer_definition_fails_before_starting_the_cli()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new DevcontainerTestWorkspace();
        File.Delete(Path.Combine(workspace.RootPath, ".devcontainer", "devcontainer.json"));

        // Act
        var (exitCode, _, error) = await workspace.Run(["--devcontainer-cli", workspace.CliPath]);

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldContain("Could not find .devcontainer/devcontainer.json under", StringComparison.Ordinal);
        workspace.CliCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Missing_docker_fails_before_starting_the_cli()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new DevcontainerTestWorkspace();

        // Act
        var (exitCode, _, error) = await workspace.Run(
            ["--devcontainer-cli", workspace.CliPath],
            workspace.Host() with
            {
                DockerExecutable = null
            });

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldContain("docker", StringComparison.Ordinal);
        workspace.CliCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Cli_bootstrap_requires_a_linux_x64_host()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new DevcontainerTestWorkspace();
        using var handler = new TestHttpMessageHandler();
        using var httpClient = new HttpClient(handler);

        // Act
        var (exitCode, _, error) = await workspace.Run([], workspace.Host(isLinuxX64: false, httpClient));

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldContain("only on Linux x64", StringComparison.Ordinal);
        error.ShouldContain("--devcontainer-cli", StringComparison.Ordinal);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Cancellation_kills_the_cli_process_tree()
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var workspace = new DevcontainerTestWorkspace();
        var pidPath = Path.Combine(workspace.ToolsPath, "grandchild.pid");
        workspace.WriteCli($"sleep 60 & echo $! > '{pidPath}'; wait");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var running = workspace.Run(["--devcontainer-cli", workspace.CliPath], cancellationToken: cancellation.Token);
        var grandchild = LinuxProcessIdentity.Capture(
            await ChildProcessId.Read(pidPath, running, TestContext.Current.CancellationToken));

        // Act
        await cancellation.CancelAsync();
        var (exitCode, _, _) = await running.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        var exited = await grandchild.WaitForExit(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        exited.ShouldBeTrue();
    }

    [Theory]
    [InlineData("verify-devcontainer")]
    [InlineData("verify-devcontainer --exec \"dotnet --version\" --exec \"git --version\" --root .")]
    [InlineData("verify-devcontainer --log-directory TestResults/devcontainer-smoke --keep-container --devcontainer-cli /usr/bin/devcontainer")]
    public void Command_lines_parse_to_verify_devcontainer(string commandLine)
    {
        // Arrange
        var rootCommand = RepoConfigCommandLine.Create(new RepoConfigToolEnvironment(Path.GetTempPath()));

        // Act
        var parseResult = rootCommand.Parse(DocumentedCommandLines.Arguments("sharedkernel-repo " + commandLine));

        // Assert
        parseResult.Errors.ShouldBeEmpty();
        parseResult.CommandResult.Command.Name.ShouldBe("verify-devcontainer");
    }

    [Theory]
    [InlineData("--exec", "")]
    [InlineData("--exec", " ")]
    [InlineData("--log-directory", "")]
    [InlineData("--devcontainer-cli", " ")]
    public void Blank_option_values_are_parse_errors(string option, string value)
    {
        // Arrange
        var rootCommand = RepoConfigCommandLine.Create(new RepoConfigToolEnvironment(Path.GetTempPath()));

        // Act
        var parseResult = rootCommand.Parse(["verify-devcontainer", option, value]);

        // Assert
        parseResult.Errors.ShouldNotBeEmpty();
    }
}
