using System.Text.Json;

namespace SharedKernel.RepoConfig.Tests;

/// <summary>Checks the Linux budget command's real literal process boundary.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class ResourceBudgetCommandTests
{
    /// <summary>Gets whether Linux process fixtures can run.</summary>
    public static bool Linux => OperatingSystem.IsLinux();

    [Fact]
    public async Task Help_describes_the_opt_in_budget_command()
    {
        // Arrange
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var exitCode = await RepoConfigToolRunner.Run(
            ["run-with-budget", "--help"], output, error, Environment.CurrentDirectory, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        output.ToString().ShouldContain("--minimum-host-memory-mib", StringComparison.Ordinal);
        output.ToString().ShouldContain("--minimum-cgroup-headroom-mib", StringComparison.Ordinal);
        error.ToString().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("--sample-seconds", "0", "--sample-seconds must be between 1 and 4294967.")]
    [InlineData("--sample-seconds", "4294968", "--sample-seconds must be between 1 and 4294967.")]
    [InlineData("--maximum-memory-pressure-percent", "-1", "--maximum-memory-pressure-percent must be between 0 and 100.")]
    [InlineData("--maximum-memory-pressure-percent", "101", "--maximum-memory-pressure-percent must be between 0 and 100.")]
    [InlineData("--pressure-samples", "1", "--pressure-samples must be at least 2.")]
    [InlineData("--minimum-host-memory-mib", "0", "--minimum-host-memory-mib requires a positive whole MiB value")]
    [InlineData("--minimum-scratch-mib", "8796093022208", "--minimum-scratch-mib requires a positive whole MiB value")]
    public void Invalid_thresholds_fail_parsing(string option, string value, string diagnostic)
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        Dictionary<string, string> options = new(StringComparer.Ordinal)
        {
            ["--working-directory"] = directory.Path,
            ["--scratch"] = directory.Path,
            ["--report"] = Path.Combine(directory.Path, "budget.json"),
            ["--minimum-host-memory-mib"] = "1",
            ["--minimum-scratch-mib"] = "1",
            ["--sample-seconds"] = "1",
        };
        options[option] = value;
        string[] arguments = ["run-with-budget", .. options.SelectMany(static pair => new[] { pair.Key, pair.Value }), "--", "fixture"];

        // Act
        var result = RepoConfigCommandLine.Create(new RepoConfigToolEnvironment(directory.Path)).Parse(arguments);

        // Assert
        var error = result.Errors.ShouldHaveSingleItem();
        error.Message.ShouldContain(diagnostic, StringComparison.Ordinal);
    }

    [Theory(SkipUnless = nameof(Linux), Skip = "The budget host check requires Linux.")]
    [InlineData("--working-directory", "relative-work")]
    [InlineData("--scratch", "missing-scratch")]
    [InlineData("--report", "relative-report")]
    public async Task Invalid_paths_fail_before_reserving_the_report_or_launching(string option, string kind)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var directory = new TemporaryDirectory();
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();
        var reportPath = Path.Combine(directory.Path, "budget.json");
        var marker = Path.Combine(directory.Path, "child-started");
        string[] arguments =
        [
            "run-with-budget", "--working-directory", option == "--working-directory" ? kind : directory.Path,
            "--scratch", option == "--scratch" ? Path.Combine(directory.Path, kind) : directory.Path,
            "--report", option == "--report" ? kind : reportPath,
            "--minimum-host-memory-mib", "1", "--minimum-scratch-mib", "1", "--sample-seconds", "1",
            "--", "/bin/sh", "-c", """printf 'unexpected' > "$1" """, "fixture", marker,
        ];

        // Act
        var exitCode = await RepoConfigToolRunner.Run(arguments, output, error, directory.Path, ct);

        // Assert
        exitCode.ShouldBe(1);
        error.ToString().ShouldContain(option, StringComparison.Ordinal);
        File.Exists(reportPath).ShouldBeFalse();
        File.Exists(marker).ShouldBeFalse();
    }

    [Fact(SkipUnless = nameof(Linux), Skip = "The literal process fixture requires Linux.")]
    public async Task Runs_literal_arguments_and_preserves_the_exit_with_bounded_redacted_evidence()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var directory = new TemporaryDirectory();
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();
        var reportPath = Path.Combine(directory.Path, "budget.json");
        var scratch = Directory.CreateDirectory(Path.Combine(directory.Path, "scratch")).FullName;
        const string literal = "literal argument; with spaces";
        string[] arguments =
        [
            "run-with-budget", "--working-directory", directory.Path, "--scratch", scratch, "--report", reportPath,
            "--minimum-host-memory-mib", "1", "--minimum-scratch-mib", "1", "--sample-seconds", "1",
            "--", "/bin/sh", "-c", """[ . -ef "$2" ] && [ "$TMPDIR" = "$3" ] && [ "$1" = "literal argument; with spaces" ] && exit 37""",
            "fixture", literal, directory.Path, scratch,
        ];

        // Act
        var exitCode = await RepoConfigToolRunner.Run(arguments, output, error, directory.Path, ct);

        // Assert
        exitCode.ShouldBe(37);
        error.ToString().ShouldBeEmpty();
        var raw = await File.ReadAllTextAsync(reportPath, ct);
        raw.Length.ShouldBeLessThan(2048);
        raw.ShouldNotContain(literal, StringComparison.Ordinal);
        raw.ShouldNotContain(directory.Path, StringComparison.Ordinal);
        using var report = JsonDocument.Parse(raw);
        report.RootElement.GetProperty("reason").GetString().ShouldBe("None");
        report.RootElement.GetProperty("childStarted").GetBoolean().ShouldBeTrue();
        report.RootElement.GetProperty("childExitCode").GetInt32().ShouldBe(37);
    }

    [Fact(SkipUnless = nameof(Linux), Skip = "The host preflight fixture requires Linux.")]
    public async Task Refused_preflight_records_the_reason_before_running_the_executable()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var directory = new TemporaryDirectory();
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();
        var reportPath = Path.Combine(directory.Path, "budget.json");
        var marker = Path.Combine(directory.Path, "child-started");
        string[] arguments =
        [
            "run-with-budget", "--working-directory", directory.Path, "--scratch", directory.Path, "--report", reportPath,
            "--minimum-host-memory-mib", "8000000000000", "--minimum-scratch-mib", "1", "--sample-seconds", "1",
            "--", "/bin/sh", "-c", """printf 'unexpected' > "$1" """, "fixture", marker,
        ];

        // Act
        var exitCode = await RepoConfigToolRunner.Run(arguments, output, error, directory.Path, ct);

        // Assert
        exitCode.ShouldBe(125);
        File.Exists(marker).ShouldBeFalse();
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(reportPath, ct));
        report.RootElement.GetProperty("reason").GetString().ShouldBe("HostMemoryFloor");
        report.RootElement.GetProperty("childStarted").GetBoolean().ShouldBeFalse();
        report.RootElement.GetProperty("sampleCount").GetInt32().ShouldBe(1);
    }

    [Fact(SkipUnless = nameof(Linux), Skip = "The host preflight fixture requires Linux.")]
    public async Task Preflight_report_preserves_independent_opt_in_limits()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var directory = new TemporaryDirectory();
        var reportPath = Path.Combine(directory.Path, "budget.json");
        string[] arguments =
        [
            "run-with-budget", "--working-directory", directory.Path, "--scratch", directory.Path, "--report", reportPath,
            "--minimum-host-memory-mib", "8000000000000", "--minimum-scratch-mib", "2", "--sample-seconds", "1",
            "--minimum-cgroup-headroom-mib", "3", "--maximum-memory-pressure-percent", "100", "--pressure-samples", "3",
            "--reject-memory-filesystem", "--", "/bin/true",
        ];

        // Act
        var exitCode = await RepoConfigToolRunner.Run(arguments, TextWriter.Null, TextWriter.Null, directory.Path, ct);

        // Assert
        exitCode.ShouldBe(125);
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(reportPath, ct));
        report.RootElement.GetProperty("childStarted").GetBoolean().ShouldBeFalse();
        var limits = report.RootElement.GetProperty("limits");
        limits.GetProperty("minimumCgroupHeadroomBytes").GetInt64().ShouldBe(3 * 1024 * 1024L);
        limits.GetProperty("minimumScratchBytes").GetInt64().ShouldBe(2 * 1024 * 1024L);
        limits.GetProperty("maximumMemoryPressurePercent").GetDecimal().ShouldBe(100m);
        limits.GetProperty("pressureSamples").GetInt32().ShouldBe(3);
        limits.GetProperty("rejectMemoryFilesystem").GetBoolean().ShouldBeTrue();
    }

    [Theory(SkipUnless = nameof(Linux), Skip = "The launch fixture requires Linux.")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_command_preserves_a_redacted_launch_failure(bool absoluteExecutable)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var directory = new TemporaryDirectory();
        var reportPath = Path.Combine(directory.Path, "budget.json");
        const string missing = "absent-resource-budget-executable-9a463fd5";
        var executable = absoluteExecutable ? Path.Combine(directory.Path, missing) : missing;
        string[] arguments =
        [
            "run-with-budget", "--working-directory", directory.Path, "--scratch", directory.Path, "--report", reportPath,
            "--minimum-host-memory-mib", "1", "--minimum-scratch-mib", "1", "--sample-seconds", "1",
            "--", executable, "test-private-arg",
        ];

        // Act
        var exitCode = await RepoConfigToolRunner.Run(arguments, TextWriter.Null, TextWriter.Null, directory.Path, ct);

        // Assert
        exitCode.ShouldBe(125);
        var raw = await File.ReadAllTextAsync(reportPath, ct);
        raw.ShouldNotContain(missing, StringComparison.Ordinal);
        raw.ShouldNotContain("test-private-arg", StringComparison.Ordinal);
        using var report = JsonDocument.Parse(raw);
        report.RootElement.GetProperty("reason").GetString().ShouldBe("LaunchFailed");
        report.RootElement.GetProperty("childStarted").GetBoolean().ShouldBeFalse();
        report.RootElement.GetProperty("childExitCode").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Theory(SkipUnless = nameof(Linux), Skip = "The process-tree fixture requires Linux.")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancelled_command_reports_observed_process_cleanup(bool cooperate)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var directory = new TemporaryDirectory();
        var reportPath = Path.Combine(directory.Path, "budget.json");

        // Act
        var exitCode = await ResourceBudgetProcessFixture.RunBudgetCommandUntilDescendantStarts(directory.Path, reportPath, cooperate, ct);

        // Assert
        exitCode.ShouldBe(130);
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(reportPath, ct));
        var result = report.RootElement;
        result.GetProperty("reason").GetString().ShouldBe("Cancelled");
        result.GetProperty("childStarted").GetBoolean().ShouldBeTrue();
        var cleanup = result.GetProperty("cleanup");
        cleanup.GetProperty("observationComplete").GetBoolean().ShouldBeTrue();
        cleanup.GetProperty("observedProcesses").GetInt32().ShouldBeGreaterThanOrEqualTo(2);
        cleanup.GetProperty("remainingObservedProcesses").GetInt32().ShouldBe(0);
        if (cooperate)
        {
            cleanup.GetProperty("isolatedPodmanTeardown").GetString().ShouldBe("NoActiveEngine");
        }
        else
        {
            cleanup.TryGetProperty("isolatedPodmanTeardown", out _).ShouldBeFalse();
        }
        Directory.EnumerateDirectories(directory.Path).ShouldBeEmpty();
    }

    [Theory(SkipUnless = nameof(Linux), Skip = "The executable fixture requires Linux.")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Executable_preserves_a_natural_exit_with_or_without_cooperative_cleanup(bool cooperate)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var directory = new TemporaryDirectory();
        var reportPath = Path.Combine(directory.Path, "budget.json");
        string[] arguments =
        [
            typeof(RepoConfigCommandLine).Assembly.Location, "run-with-budget", "--working-directory", directory.Path,
            "--scratch", directory.Path, "--report", reportPath, "--minimum-host-memory-mib", "1",
            "--minimum-scratch-mib", "1", "--sample-seconds", "1",
            .. cooperate ? ["--cooperative-podman-cleanup"] : Array.Empty<string>(),
            "--", "/bin/true",
        ];

        // Act
        var result = await TestProcess.Run("dotnet", directory.Path, arguments, ct);

        // Assert
        result.ExitCode.ShouldBe(0, result.StandardError);
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(reportPath, ct));
        report.RootElement.GetProperty("reason").GetString().ShouldBe("None");
        report.RootElement.GetProperty("cleanup").ValueKind.ShouldBe(JsonValueKind.Null);
        Directory.EnumerateDirectories(directory.Path).ShouldBeEmpty();
    }

    [Fact(SkipUnless = nameof(Linux), Skip = "The process-tree fixture requires Linux.")]
    public async Task Cancellation_checks_the_owned_child_and_its_live_descendant()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var directory = new TemporaryDirectory();

        // Act
        var outcome = await ResourceBudgetProcessFixture.RunUntilDescendantStarts(directory.Path, ct);

        // Assert
        outcome.Started.ShouldBeTrue();
        outcome.ExitCode.ShouldBeNull();
        var cleanup = outcome.Cleanup.ShouldNotBeNull();
        cleanup.ObservationComplete.ShouldBeTrue();
        cleanup.ObservedProcesses.ShouldBeGreaterThanOrEqualTo(2);
        cleanup.RemainingObservedProcesses.ShouldBe(0);
    }
}
