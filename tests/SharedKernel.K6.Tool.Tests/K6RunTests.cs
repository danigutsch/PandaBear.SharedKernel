namespace PandaBear.K6.Tool.Tests;

public sealed class K6RunTests
{
    [Fact]
    public async Task Local_k6_receives_the_controlled_arguments_and_its_exit_code_is_returned()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = new K6TestRepository();
        var k6 = repository.AddFake("k6", exitCode: 99);
        repository.SetEnvironment("APP_BASE_URL", "http://localhost:5000/");
        repository.SetEnvironment("APP_PROFILE", "load");

        // Act
        var exitCode = await repository.Invoke(["run", "--scenario", "smoke", "--", "--vus", "2"], TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(99);
        var run = repository.RunDirectories().ShouldHaveSingleItem();
        Path.GetFileName(run).ShouldMatch(@"\Asmoke-\d{8}T\d{6}Z-[0-9a-f]{8}\z");
        k6.Arguments().ShouldBe(
        [
            "run",
            "--no-usage-report",
            "--summary-export",
            Path.Combine(run, "k6-summary.json"),
            "-e",
            "APP_BASE_URL=http://localhost:5000",
            "-e",
            "K6_NO_USAGE_REPORT=true",
            "-e",
            "APP_PROFILE=load",
            "--vus",
            "2",
            Path.Combine(repository.Root, "perf", "k6", "scenarios", "smoke.js"),
        ]);
        repository.Error.ToString().ShouldBe("Results: perf/results/" + Path.GetFileName(run) + Environment.NewLine);
        repository.Output.ToString().ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Local_k6_runs_with_a_minimal_environment()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = new K6TestRepository();
        var k6 = repository.AddFake("k6");
        repository.SetEnvironment("APP_BASE_URL", "http://127.0.0.1:5000");

        // Act
        var exitCode = await repository.Invoke(["run", "--scenario", "smoke"], TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        k6.Environment().Where(static line => !line.StartsWith("PWD=", StringComparison.Ordinal) && !line.StartsWith("SHLVL=", StringComparison.Ordinal) && !line.StartsWith("_=", StringComparison.Ordinal))
            .ShouldBe([]);
    }

    [Theory]
    [InlineData("podman", new string[0])]
    [InlineData("docker", new[] { "--add-host", "host.docker.internal:host-gateway" })]
    public async Task Container_runs_mount_the_scripts_read_only_and_reach_the_target_through_the_host_alias(string engine, string[] hostArguments)
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = new K6TestRepository(K6TestRepository.DefaultConfiguration.Replace("\"podman\"", "\"" + engine + "\"", StringComparison.Ordinal));
        var fake = repository.AddFake(engine, exitCode: 7);
        repository.SetEnvironment("APP_BASE_URL", "http://localhost:5000");
        repository.SetEnvironment("APP_VUS", "3");

        // Act
        var exitCode = await repository.Invoke(["run", "--scenario", "smoke", "--container", "--", "--duration", "5s"], TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(7);
        var run = repository.RunDirectories().ShouldHaveSingleItem();
        var arguments = fake.Arguments();
        arguments[4].ShouldMatch(@"\Asharedkernel-k6-[0-9a-f]{32}\z");
        arguments.ShouldBe(
        [
            "run",
            "--rm",
            "--pull=never",
            "--name",
            arguments[4],
            .. hostArguments,
            "--mount",
            "type=bind,source=" + Path.Combine(repository.Root, "perf", "k6") + ",target=/k6,readonly",
            "--mount",
            "type=bind,source=" + run + ",target=/results",
            "-w",
            "/k6",
            K6TestRepository.Image,
            "run",
            "--no-usage-report",
            "--summary-export",
            "/results/k6-summary.json",
            "-e",
            "APP_BASE_URL=http://host.docker.internal:5000",
            "-e",
            "K6_NO_USAGE_REPORT=true",
            "-e",
            "APP_VUS=3",
            "--duration",
            "5s",
            "/k6/scenarios/smoke.js",
        ]);
    }

    [Fact]
    public async Task Container_runs_create_a_results_directory_that_the_container_user_can_write()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = new K6TestRepository();
        repository.AddFake("podman", body: K6TestRepository.WritesContainerSummary);
        repository.SetEnvironment("APP_BASE_URL", "http://localhost:5000");

        // Act
        var exitCode = await repository.Invoke(["run", "--scenario", "smoke", "--container"], TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var run = repository.RunDirectories().ShouldHaveSingleItem();
        File.GetUnixFileMode(run).ShouldBe(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute
            | UnixFileMode.StickyBit);
        File.GetUnixFileMode(repository.ResultsDirectory).HasFlag(UnixFileMode.OtherWrite).ShouldBeFalse();
    }

    [Fact]
    public async Task Local_runs_leave_the_mode_of_the_results_directory_alone()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = new K6TestRepository();
        repository.AddFake("k6");
        repository.SetEnvironment("APP_BASE_URL", "http://localhost:5000");

        // Act
        var exitCode = await repository.Invoke(["run", "--scenario", "smoke"], TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var mode = File.GetUnixFileMode(repository.RunDirectories().ShouldHaveSingleItem());
        mode.HasFlag(UnixFileMode.OtherWrite).ShouldBeFalse();
        mode.HasFlag(UnixFileMode.StickyBit).ShouldBeFalse();
    }

    [Fact]
    public async Task A_container_run_that_exits_0_without_a_summary_fails()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = new K6TestRepository();
        repository.AddFake("podman");
        repository.SetEnvironment("APP_BASE_URL", "http://localhost:5000");

        // Act
        var exitCode = await repository.Invoke(["run", "--scenario", "smoke", "--container"], TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(2);
        repository.Error.ToString().ShouldContain("exited 0 but wrote no k6-summary.json", StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_image_option_replaces_the_configured_image()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = new K6TestRepository();
        var podman = repository.AddFake("podman", body: K6TestRepository.WritesContainerSummary);
        repository.SetEnvironment("APP_BASE_URL", "http://localhost:5000");
        const string image = "mirror.example/k6@sha256:fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";

        // Act
        var exitCode = await repository.Invoke(["run", "--scenario", "smoke", "--container", "--image", image], TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        podman.Arguments().ShouldContain(image);
        podman.Arguments().ShouldNotContain(K6TestRepository.Image);
    }

    [Theory]
    [InlineData(null, "APP_BASE_URL is required.")]
    [InlineData("https://example.com", "APP_BASE_URL must target a loopback host such as localhost, 127.0.0.1, or [::1] unless --allow-external-target is passed.")]
    public async Task Targets_are_validated_before_k6_starts(string? target, string message)
    {
        // Arrange
        using var repository = new K6TestRepository();
        repository.SetExecutable("k6", "/nonexistent/k6");
        repository.SetEnvironment("APP_BASE_URL", target);

        // Act
        var exitCode = await repository.Invoke(["run", "--scenario", "smoke"], TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(2);
        repository.Error.ToString().ShouldBe(message + Environment.NewLine);
        repository.RunDirectories().ShouldBe([]);
    }

    [Fact]
    public async Task External_targets_run_when_allowed()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = new K6TestRepository();
        var k6 = repository.AddFake("k6");
        repository.SetEnvironment("APP_BASE_URL", "https://staging.example");

        // Act
        var exitCode = await repository.Invoke(["run", "--scenario", "smoke", "--allow-external-target"], TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        k6.Arguments().ShouldContain("APP_BASE_URL=https://staging.example");
    }

    [Fact]
    public async Task Blocked_k6_arguments_fail_before_k6_starts()
    {
        // Arrange
        using var repository = new K6TestRepository();
        repository.SetExecutable("k6", "/nonexistent/k6");
        repository.SetEnvironment("APP_BASE_URL", "http://localhost:5000");

        // Act
        var exitCode = await repository.Invoke(["run", "--scenario", "smoke", "--", "-e", "APP_PROFILE=stress"], TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(2);
        repository.Error.ToString().ShouldBe("-e and --env cannot set APP_PROFILE, which the tool sets from the scenario configuration." + Environment.NewLine);
        repository.RunDirectories().ShouldBe([]);
    }

    [Theory]
    [InlineData("k6", new string[0])]
    [InlineData("podman", new[] { "--container" })]
    public async Task A_missing_runner_fails(string runner, string[] options)
    {
        // Arrange
        using var repository = new K6TestRepository();
        repository.SetEnvironment("APP_BASE_URL", "http://localhost:5000");

        // Act
        var exitCode = await repository.Invoke(["run", "--scenario", "smoke", .. options], TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(2);
        repository.Error.ToString().ShouldBe(runner + " was not found in an absolute PATH directory." + Environment.NewLine);
    }

    [Fact]
    public async Task A_missing_script_fails()
    {
        // Arrange
        using var repository = new K6TestRepository();
        repository.SetExecutable("k6", "/nonexistent/k6");
        repository.SetEnvironment("APP_BASE_URL", "http://localhost:5000");
        File.Delete(Path.Combine(repository.Root, "perf", "k6", "scenarios", "smoke.js"));

        // Act
        var exitCode = await repository.Invoke(["run", "--scenario", "smoke"], TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(2);
        repository.Error.ToString().ShouldBe("The script perf/k6/scenarios/smoke.js of scenario smoke does not exist." + Environment.NewLine);
    }

    [Fact]
    public async Task A_symbolic_link_in_the_results_directory_fails()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = new K6TestRepository();
        using var outside = new TemporaryDirectory();
        repository.SetExecutable("k6", "/nonexistent/k6");
        repository.SetEnvironment("APP_BASE_URL", "http://localhost:5000");
        File.CreateSymbolicLink(repository.ResultsDirectory, outside.Path);

        // Act
        var exitCode = await repository.Invoke(["run", "--scenario", "smoke"], TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(2);
        repository.Error.ToString().ShouldBe("perf/results: Symbolic links are not followed in repository paths." + Environment.NewLine);
        Directory.GetFileSystemEntries(outside.Path).ShouldBe([]);
    }

    [Fact]
    public async Task Cancellation_kills_k6_and_exits_1()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = new K6TestRepository();
        var k6 = repository.AddFake("k6", body: "exec /bin/sleep 300");
        repository.SetEnvironment("APP_BASE_URL", "http://localhost:5000");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var watchdog = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        watchdog.CancelAfter(TimeSpan.FromSeconds(30));

        // Act
        var run = repository.Invoke(["run", "--scenario", "smoke"], cancellation.Token);
        while (!File.Exists(k6.EnvironmentPath) && !run.IsCompleted)
        {
            await Task.Delay(20, watchdog.Token);
        }

        var processId = k6.ProcessId();
        await cancellation.CancelAsync();
        var exitCode = await run;

        // Assert
        exitCode.ShouldBe(1);
        ProcessState.IsRunning(processId).ShouldBeFalse();
    }
}
