using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.Json;

namespace PandaBear.Repository.Tests;

/// <summary>
/// Guards <c>run</c> for a slice with a service: the order of the phases, who sees the service's value, and that the service is
/// stopped, its ready file removed, and its failures reported whatever else fails. The service is a fake shell script.
/// </summary>
[UnsupportedOSPlatform("windows")]
public sealed class SliceServiceTests
{
    private const string Database = ServiceRunScope.DatabaseHost;

    private static readonly string HostReady = FakeServiceHost.Ready(ServiceRunScope.Value) + FakeServiceHost.WaitForEndOfInput;

    [PosixFact]
    public async Task The_service_starts_after_the_build_serves_the_test_phase_and_is_stopped_after_it()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var run = new ServiceRunScope(HostReady);
        (bool Running, bool ReadyFileExists, UnixFileMode DirectoryMode)? during = null;
        run.Respond = (fileName, arguments, _) =>
        {
            if (arguments is ["test", ..])
            {
                during = (run.Host.IsRunning(Database), File.Exists(run.ReadyFile), File.GetUnixFileMode(Path.GetDirectoryName(run.ReadyFile)!));
            }

            return Task.FromResult(fileName == "git" ? "fixture" : string.Empty);
        };

        // Act
        var result = await run.Run(["run", "--slice", "database"], ct);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        var filter = Path.Combine(run.Execution, "tests.slnf");
        run.Processes.Commands.ShouldBe(
        [
            "git rev-parse HEAD",
            "dotnet restore " + filter + " --locked-mode",
            "dotnet build " + filter + " -c Release --no-restore",
            "dotnet test --solution " + filter + " -c Release --no-build --no-restore --results-directory " + Path.Combine(run.Execution, "results"),
        ]);
        (await ServiceRunScope.Phases(run.Execution, ct)).ShouldBe(
            ["load passed", "restore passed", "build passed", "service-database passed", "test passed", "service-stop-database passed", "total passed"]);
        during.ShouldBe((true, true, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute), "The service runs and its private ready file exists while the tests run.");
        run.Host.IsRunning(Database).ShouldBeFalse();
    }

    [PosixFact]
    public async Task Only_the_test_phase_receives_the_value_and_the_service_receives_only_the_ready_file()
    {
        // Arrange
        using var run = new ServiceRunScope(HostReady);

        // Act
        var result = await run.Run(["run", "--slice", "database"], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        var receiving = run.Processes.Calls.Where(static call => call.Environment?.ContainsKey("FAKE_SERVICE_VALUE") == true).ShouldHaveSingleItem();
        receiving.Arguments[0].ShouldBe("test");
        receiving.Environment!["FAKE_SERVICE_VALUE"].ShouldBe(ServiceRunScope.Value);
        receiving.Environment.ContainsKey("FAKE_READY_FILE").ShouldBeFalse("The ready file belongs to the service alone.");
        var service = run.Host.Environment(Database);
        service["FAKE_READY_FILE"].ShouldEndWith("ready", StringComparison.Ordinal);
        service.ContainsKey("FAKE_SERVICE_VALUE").ShouldBeFalse("The service never receives the value it hands over.");
    }

    [PosixFact]
    public async Task The_value_is_in_no_file_and_no_message_even_when_the_tests_print_it()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var run = new ServiceRunScope(HostReady);
        run.Respond = (fileName, arguments, _) => Task.FromResult(
            fileName == "git" ? "fixture" : arguments is ["test", ..] ? "connected to " + ServiceRunScope.Value + Environment.NewLine : string.Empty);

        // Act
        var result = await run.Run(["run", "--slice", "database"], ct);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        (await run.FilesWith(ServiceRunScope.Value, ct)).ShouldBeEmpty();
        result.Output.ShouldNotContain(ServiceRunScope.Value, StringComparison.Ordinal);
        result.Error.ShouldNotContain(ServiceRunScope.Value, StringComparison.Ordinal);
        result.Error.ShouldContain("connected to [redacted]", StringComparison.Ordinal);
        (await File.ReadAllTextAsync(Path.Combine(run.Execution, "test.log"), ct)).ShouldBe("connected to [redacted]" + Environment.NewLine);
    }

    [PosixFact]
    public async Task The_value_is_in_no_file_and_no_message_when_the_tests_fail_and_print_it()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var run = new ServiceRunScope(HostReady);
        run.Respond = (fileName, arguments, _) => fileName == "git"
            ? Task.FromResult("fixture")
            : arguments is ["test", ..] ? throw new InvalidOperationException("Command failed (1): dotnet test" + Environment.NewLine + ServiceRunScope.Value) : Task.FromResult(string.Empty);

        // Act
        var result = await run.Run(["run", "--slice", "database"], ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        (await run.FilesWith(ServiceRunScope.Value, ct)).ShouldBeEmpty();
        result.Output.ShouldNotContain(ServiceRunScope.Value, StringComparison.Ordinal);
        result.Error.ShouldNotContain(ServiceRunScope.Value, StringComparison.Ordinal);
        result.Error.ShouldContain("Command failed (1): dotnet test", StringComparison.Ordinal);
        result.Error.ShouldContain("[redacted]", StringComparison.Ordinal);
    }

    [PosixFact]
    public async Task The_ready_file_is_in_a_private_directory_outside_the_execution_directory_and_is_deleted()
    {
        // Arrange
        using var run = new ServiceRunScope(HostReady);

        // Act
        var result = await run.Run(["run", "--slice", "database"], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        var readyFile = run.ReadyFile;
        Path.IsPathFullyQualified(readyFile).ShouldBeTrue();
        Path.GetRelativePath(run.Execution, readyFile).ShouldStartWith("..", StringComparison.Ordinal, "The execution directory is uploaded as a public artifact.");
        Path.GetRelativePath(run.Repository.Root, readyFile).ShouldStartWith("..", StringComparison.Ordinal);
        File.Exists(readyFile).ShouldBeFalse();
        Directory.Exists(Path.GetDirectoryName(readyFile)).ShouldBeFalse();
    }

    [PosixFact]
    public async Task The_service_output_streams_to_log_files_that_the_phase_names()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var run = new ServiceRunScope("echo host-out\necho host-err >&2\n" + HostReady);

        // Act
        var result = await run.Run(["run", "--slice", "database"], ct);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        (await File.ReadAllTextAsync(Path.Combine(run.Execution, "service-database.stdout.log"), ct)).ShouldBe("host-out\n");
        (await File.ReadAllTextAsync(Path.Combine(run.Execution, "service-database.stderr.log"), ct)).ShouldBe("host-err\n");
        var phase = await File.ReadAllTextAsync(Path.Combine(run.Execution, "service-database.log"), ct);
        phase.ShouldContain("service-database.stdout.log", StringComparison.Ordinal);
        phase.ShouldContain("service-database.stderr.log", StringComparison.Ordinal);
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(run.Execution, "manifest.json"), ct));
        manifest.RootElement.GetProperty("phases").EnumerateArray().Select(static phase => phase.GetProperty("name").GetString())
            .ShouldBe(["load", "restore", "build", "service-database", "test", "service-stop-database"]);
    }

    [PosixFact]
    public async Task A_service_that_exits_before_it_is_ready_fails_the_run_with_its_exit_code_and_its_log_files()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var run = new ServiceRunScope("echo boom >&2\nexit 4\n");

        // Act
        var result = await run.Run(["run", "--slice", "database"], ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("The service of slice database exited with code 4 before it was ready.", StringComparison.Ordinal);
        result.Error.ShouldContain("TestResults/selection/execution-database/service-database.stdout.log", StringComparison.Ordinal);
        result.Error.ShouldContain("TestResults/selection/execution-database/service-database.stderr.log", StringComparison.Ordinal);
        run.Processes.Commands.ShouldNotContain(static command => command.StartsWith("dotnet test", StringComparison.Ordinal));
        (await File.ReadAllTextAsync(Path.Combine(run.Execution, "service-database.stderr.log"), ct)).ShouldBe("boom\n");
        (await ServiceRunScope.Phases(run.Execution, ct)).ShouldBe(
            ["load passed", "restore passed", "build passed", "service-database failed", "service-stop-database passed", "total failed"],
            "The exit code is reported once, by the phase that waited for the service.");
        Directory.Exists(Path.GetDirectoryName(run.ReadyFile)).ShouldBeFalse();
    }

    [PosixFact]
    public async Task A_service_that_never_writes_its_value_times_out_and_is_stopped()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var run = new ServiceRunScope(FakeServiceHost.WaitForEndOfInput, readyTimeoutSeconds: 1);

        // Act
        var result = await run.Run(["run", "--slice", "database"], ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("The service of slice database did not write FAKE_READY_FILE within 1 seconds.", StringComparison.Ordinal);
        run.Processes.Commands.ShouldNotContain(static command => command.StartsWith("dotnet test", StringComparison.Ordinal));
        run.Host.IsRunning(Database).ShouldBeFalse();
        (await ServiceRunScope.Phases(run.Execution, ct)).ShouldBe(
            ["load passed", "restore passed", "build passed", "service-database failed", "service-stop-database passed", "total failed"]);
        Directory.Exists(Path.GetDirectoryName(run.ReadyFile)).ShouldBeFalse();
    }

    [PosixFact]
    public async Task A_ready_file_larger_than_four_kibibytes_fails_the_run_without_repeating_it()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var run = new ServiceRunScope(
            "head -c 5000 /dev/zero | tr '\\0' 'a' > \"$FAKE_READY_FILE.tmp\"\nmv \"$FAKE_READY_FILE.tmp\" \"$FAKE_READY_FILE\"\n" + FakeServiceHost.WaitForEndOfInput);

        // Act
        var result = await run.Run(["run", "--slice", "database"], ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("The ready file of the service of slice database is larger than 4096 bytes.", StringComparison.Ordinal);
        result.Error.ShouldNotContain("aaaaaaaa", StringComparison.Ordinal);
        run.Processes.Commands.ShouldNotContain(static command => command.StartsWith("dotnet test", StringComparison.Ordinal));
        run.Host.IsRunning(Database).ShouldBeFalse();
        (await ServiceRunScope.Phases(run.Execution, ct)).ShouldBe(
            ["load passed", "restore passed", "build passed", "service-database failed", "service-stop-database passed", "total failed"]);
    }

    [PosixFact]
    public async Task A_value_with_a_control_character_fails_the_run_without_repeating_it()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var run = new ServiceRunScope("printf 'canary\\tvalue\\n' > \"$FAKE_READY_FILE.tmp\"\nmv \"$FAKE_READY_FILE.tmp\" \"$FAKE_READY_FILE\"\n" + FakeServiceHost.WaitForEndOfInput);

        // Act
        var result = await run.Run(["run", "--slice", "database"], ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("The ready file of the service of slice database must hold one line without control characters.", StringComparison.Ordinal);
        result.Error.ShouldNotContain("canary", StringComparison.Ordinal);
        (await run.FilesWith("canary", ct)).ShouldBeEmpty();
        run.Host.IsRunning(Database).ShouldBeFalse();
    }

    [PosixFact]
    public async Task Cancelling_while_the_service_starts_stops_it_and_records_the_failed_run()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var run = new ServiceRunScope(FakeServiceHost.WaitForEndOfInput);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var running = run.Run(["run", "--slice", "database"], cancellation.Token);
        while (!run.Host.HasStarted(Database))
        {
            running.IsCompleted.ShouldBeFalse("The run ended before the service started.");
            await Task.Delay(20, ct);
        }

        // Act
        await cancellation.CancelAsync();
        var result = await running.WaitAsync(TimeSpan.FromSeconds(60), ct);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldBe(string.Empty);
        result.Error.ShouldNotContain("exited with code", StringComparison.Ordinal, "A cancellation reports no failure.");
        run.Processes.Commands.ShouldNotContain(static command => command.StartsWith("dotnet test", StringComparison.Ordinal));
        run.Host.IsRunning(Database).ShouldBeFalse();
        Directory.Exists(Path.GetDirectoryName(run.ReadyFile)).ShouldBeFalse();
        (await ServiceRunScope.Phases(run.Execution, ct)).ShouldBe(
            ["load passed", "restore passed", "build passed", "service-database failed", "service-stop-database passed", "total failed"]);
    }

    [PosixFact]
    public async Task A_failed_test_phase_stops_the_service_and_stays_the_reported_failure()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var run = new ServiceRunScope(HostReady);
        run.Respond = (fileName, arguments, _) => fileName == "git"
            ? Task.FromResult("fixture")
            : arguments is ["test", ..] ? throw new InvalidOperationException("Tests failed.") : Task.FromResult(string.Empty);

        // Act
        var result = await run.Run(["run", "--slice", "database"], ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("Tests failed.", StringComparison.Ordinal);
        run.Host.IsRunning(Database).ShouldBeFalse();
        Directory.Exists(Path.GetDirectoryName(run.ReadyFile)).ShouldBeFalse();
        (await ServiceRunScope.Phases(run.Execution, ct)).ShouldBe(
            ["load passed", "restore passed", "build passed", "service-database passed", "test failed", "service-stop-database passed", "total failed"]);
    }

    [PosixFact]
    public async Task A_service_that_ignores_the_end_of_its_input_is_killed_only_after_the_stop_timeout_and_fails_the_run()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var run = new ServiceRunScope(FakeServiceHost.Ready(ServiceRunScope.Value) + "exec sleep 300\n", stopTimeoutSeconds: 1);
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await run.Run(["run", "--slice", "database"], ct);
        stopwatch.Stop();

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("The service of slice database did not exit within 1 seconds after its input closed, so its process tree was killed;", StringComparison.Ordinal);
        stopwatch.Elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromSeconds(1), "The service gets its whole stop timeout to exit.");
        run.Host.IsRunning(Database).ShouldBeFalse();
        (await ServiceRunScope.Phases(run.Execution, ct)).ShouldBe(
            ["load passed", "restore passed", "build passed", "service-database passed", "test passed", "service-stop-database failed", "total failed"]);
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(run.Execution, "manifest.json"), ct));
        manifest.RootElement.GetProperty("status").GetString().ShouldBe("failed");
    }

    [PosixFact]
    public async Task A_service_that_exits_with_an_error_after_its_input_closes_fails_an_otherwise_passing_run()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var run = new ServiceRunScope(HostReady + "exit 3\n");

        // Act
        var result = await run.Run(["run", "--slice", "database"], ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("The service of slice database exited with code 3 after its input closed.", StringComparison.Ordinal);
        (await ServiceRunScope.Phases(run.Execution, ct)).ShouldBe(
            ["load passed", "restore passed", "build passed", "service-database passed", "test passed", "service-stop-database failed", "total failed"]);
    }

    [PosixFact]
    public async Task A_service_that_crashed_while_the_tests_ran_fails_an_otherwise_passing_run()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var run = new ServiceRunScope(FakeServiceHost.Ready(ServiceRunScope.Value) + "exit 5\n");
        run.Respond = async (fileName, arguments, token) =>
        {
            if (arguments is ["test", ..])
            {
                while (run.Host.IsRunning(Database))
                {
                    await Task.Delay(20, token);
                }
            }

            return fileName == "git" ? "fixture" : string.Empty;
        };

        // Act
        var result = await run.Run(["run", "--slice", "database"], ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("The service of slice database exited with code 5 before it was stopped.", StringComparison.Ordinal);
    }

    [PosixFact]
    public async Task A_teardown_failure_is_recorded_without_replacing_the_failure_that_started_it()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var run = new ServiceRunScope(HostReady + "exit 3\n");
        run.Respond = (fileName, arguments, _) => fileName == "git"
            ? Task.FromResult("fixture")
            : arguments is ["test", ..] ? throw new InvalidOperationException("Tests failed.") : Task.FromResult(string.Empty);

        // Act
        var result = await run.Run(["run", "--slice", "database"], ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("Tests failed.", StringComparison.Ordinal);
        result.Error.ShouldNotContain("exited with code 3", StringComparison.Ordinal);
        (await ServiceRunScope.Phases(run.Execution, ct)).ShouldBe(
            ["load passed", "restore passed", "build passed", "service-database passed", "test failed", "service-stop-database failed", "total failed"]);
        var diagnostics = await File.ReadAllTextAsync(Path.Combine(run.Execution, "diagnostics.txt"), ct);
        diagnostics.ShouldContain("Tests failed.", StringComparison.Ordinal);
        diagnostics.ShouldContain("exited with code 3 after its input closed", StringComparison.Ordinal);
    }

    [PosixFact]
    public async Task A_slice_without_a_service_starts_nothing_and_runs_its_tests_as_before()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var run = new ServiceRunScope(HostReady);

        // Act
        var result = await run.Run(["run", "--slice", "library"], ct);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        run.Host.Starts.ShouldBeEmpty();
        var execution = Path.Combine(run.Selection, "execution-library");
        run.Processes.Commands.ShouldBe(
        [
            "git rev-parse HEAD",
            "dotnet restore " + Path.Combine(execution, "tests.slnf") + " --locked-mode",
            "dotnet build " + Path.Combine(execution, "tests.slnf") + " -c Release --no-restore",
            "dotnet test --solution " + Path.Combine(execution, "tests.slnf") + " -c Release --no-build --no-restore --results-directory " + Path.Combine(execution, "results"),
        ]);
        run.Processes.Calls.ShouldAllSatisfy(static call => call.Environment.ShouldBeNull("Without a service, no process is configured."));
        (await ServiceRunScope.Phases(execution, ct)).ShouldBe(["load passed", "restore passed", "build passed", "test passed", "total passed"]);
    }

    [PosixFact]
    public async Task The_service_project_is_restored_and_built_with_the_slice()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var run = new ServiceRunScope(HostReady);

        // Act
        var result = await run.Run(["run", "--slice", "database"], ct);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        (await SelectionTestFiles.Projects(run.Execution, "tests", ct)).ShouldBe(
            [Database, ServiceRunScope.DatabaseTests], "The service runs with --no-build --no-restore, so the filter must restore and build it.");
    }

    [PosixFact]
    public async Task The_service_is_started_with_dotnet_run_on_the_built_project_with_all_three_streams_redirected()
    {
        // Arrange
        using var run = new ServiceRunScope(HostReady);

        // Act
        var result = await run.Run(["run", "--slice", "database", "--configuration", "Debug"], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        var started = run.Host.Starts.ShouldHaveSingleItem();
        Path.GetFileNameWithoutExtension(started.FileName).ShouldBe("dotnet");
        Path.IsPathFullyQualified(started.FileName).ShouldBeTrue("The command resolves from the absolute directories of PATH.");
        started.Arguments.ShouldBe(["run", "--project", Database, "-c", "Debug", "--no-build", "--no-restore", "--no-launch-profile"]);
        started.WorkingDirectory.ShouldBe(run.Repository.Root);
        started.RedirectsInput.ShouldBeTrue("Closing the redirected input is how the service learns to stop.");
        started.RedirectsOutput.ShouldBeTrue();
        started.RedirectsError.ShouldBeTrue();
        started.UsesShell.ShouldBeFalse();
    }

    [PosixFact]
    public async Task Services_of_several_slices_start_in_slice_order_share_one_test_run_and_stop_in_reverse()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var run = new ServiceRunScope(HostReady);
        const string queueTests = "tests/Queue.Tests/Queue.Tests.csproj";
        const string queueHost = "tests/Queue.Host/Queue.Host.csproj";
        run.Repository.AddTestProject(queueTests, "src/Core/Core.csproj");
        run.Repository.AddProject(queueHost, "<Project />");
        run.Host.Add(queueHost, FakeServiceHost.Ready("queue-value") + FakeServiceHost.WaitForEndOfInput);
        run.Repository.Configure($$"""
            [
              { "name": "library", "projects": [] },
              { "name": "database", "projects": ["{{ServiceRunScope.DatabaseTests}}"], "service": { "project": "{{Database}}", "readyFileVariable": "FAKE_READY_FILE", "valueVariable": "FAKE_SERVICE_VALUE" } },
              { "name": "queue", "projects": ["{{queueTests}}"], "service": { "project": "{{queueHost}}", "readyFileVariable": "FAKE_READY_FILE", "valueVariable": "FAKE_QUEUE_VALUE" } }
            ]
            """);
        var selection = run.Repository.WriteSelection("fixture", ("database", [ServiceRunScope.DatabaseTests]), ("queue", [queueTests]));

        // Act
        var result = await run.Run(["run"], ct);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        (await ServiceRunScope.Phases(Path.Combine(selection, "execution"), ct)).ShouldBe(
        [
            "load passed", "restore passed", "build passed", "service-database passed", "service-queue passed", "test passed",
            "service-stop-queue passed", "service-stop-database passed", "total passed",
        ]);
        var test = run.Processes.Calls.Single(static call => call.Arguments[0] == "test");
        test.Environment!["FAKE_SERVICE_VALUE"].ShouldBe(ServiceRunScope.Value);
        test.Environment["FAKE_QUEUE_VALUE"].ShouldBe("queue-value");
        run.Host.IsRunning(Database).ShouldBeFalse();
        run.Host.IsRunning(queueHost).ShouldBeFalse();
    }
}
