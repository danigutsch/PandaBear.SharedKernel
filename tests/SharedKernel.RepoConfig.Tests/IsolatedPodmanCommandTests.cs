using System.Diagnostics;
using System.Runtime.Versioning;
using SharedKernel.Processes;

namespace SharedKernel.RepoConfig.Tests;

/// <summary>Tests the opt-in isolated Podman command.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class IsolatedPodmanCommandTests
{
    [Fact]
    public async Task Help_describes_the_opt_in_command()
    {
        // Arrange
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var exitCode = await RepoConfigToolRunner.Run(
            ["run-isolated-podman", "--help"], output, error, Environment.CurrentDirectory, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        output.ToString().ShouldContain("--image-store", StringComparison.Ordinal);
        output.ToString().ShouldContain("--cgroup-parent-slice", StringComparison.Ordinal);
        error.ToString().ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Scope_checks_reject_unsupported_hosts_before_running_the_child()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();
        var environment = new RepoConfigToolEnvironment(directory.Path)
        {
            PodmanHost = new PodmanDnsHost(null, null, directory.Path, false, static (_, _) =>
                throw new InvalidOperationException("Podman was not expected to run."))
        };

        // Act
        var exitCode = await RepoConfigToolRunner.Run(
            ["run-isolated-podman", "--image-store", directory.Path, "--", "/bin/false", "--child-argument"],
            output, error, environment, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        error.ToString().ShouldContain("requires local rootless Podman on Linux", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--timeout-seconds", "0", "--timeout-seconds must be between 1 and 4294967")]
    [InlineData("--timeout-seconds", "4294968", "--timeout-seconds must be between 1 and 4294967")]
    [InlineData("--network-helper", "other", "Argument 'other' not recognized")]
    [InlineData("--cgroup-parent-slice", "example.scope", "--cgroup-parent-slice must name a user systemd slice")]
    [InlineData("--cgroup-parent-slice", "../example.slice", "--cgroup-parent-slice must name a user systemd slice")]
    public async Task Invalid_cli_options_fail_before_probing_the_engine(string option, string value, string diagnostic)
    {
        // Arrange
        using var context = new IsolatedPodmanTestContext();
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();
        var environment = new RepoConfigToolEnvironment(context.ImageStore) { PodmanHost = context.Host };

        // Act
        var exitCode = await RepoConfigToolRunner.Run(
            ["run-isolated-podman", "--image-store", context.ImageStore, option, value, "--", context.Executable],
            output, error, environment, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        error.ToString().ShouldContain(diagnostic, StringComparison.Ordinal);
        context.Operations.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Explicit_budget_selection_without_a_slice_cannot_start_an_unbounded_run()
    {
        // Arrange
        using var context = new IsolatedPodmanTestContext();
        var runner = ScriptedProcessRunner.Returning(new ProcessOutput(0, string.Empty));
        var ct = TestContext.Current.CancellationToken;

        // Act
        var result = await context.RunCommand(["--cgroup-parent-slice"], [context.Executable], ct, runner);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("--cgroup-parent-slice", StringComparison.Ordinal);
        runner.Runs.ShouldBeEmpty();
        context.Operations.ShouldBeEmpty();
        context.Roots.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_named_slice_that_cannot_be_verified_refuses_the_run_in_both_logging_modes(bool fileLogs)
    {
        // Arrange
        const string slice = "fixture-budget.slice";
        using var context = new IsolatedPodmanTestContext();
        using var logs = new TemporaryDirectory();
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();
        List<string[]> unitQueries = [];
        var runner = ScriptedProcessRunner.Returning(new ProcessOutput(0, string.Empty));
        var environment = new RepoConfigToolEnvironment(context.ImageStore)
        {
            IsolatedPodmanCommandRunner = runner,
            PodmanHost = context.Host with
            {
                RunProcess = (startInfo, ct) =>
                {
                    if (string.Equals(Path.GetFileName(startInfo.FileName), "systemctl", StringComparison.Ordinal))
                    {
                        ct.ThrowIfCancellationRequested();
                        unitQueries.Add([.. startInfo.ArgumentList]);
                        return Task.FromResult(new PodmanCommandResult(1, string.Empty, "synthetic user manager error"));
                    }

                    return context.Host.RunProcess(startInfo, ct);
                }
            }
        };
        string[] logging = fileLogs ? ["--log-directory", logs.Path, "--max-log-bytes", "64"] : [];
        string[] command = ["run-isolated-podman", "--image-store", context.ImageStore, "--cgroup-parent-slice", slice,
            .. logging, "--", context.Executable];
        var stdout = Path.Combine(logs.Path, "stdout.log");
        var stderr = Path.Combine(logs.Path, "stderr.log");
        string[] expectedLogPaths = fileLogs ? [stderr, stdout] : [];
        var ct = TestContext.Current.CancellationToken;

        // Act
        var exitCode = await RepoConfigToolRunner.Run(command, output, error, environment, ct);

        // Assert
        exitCode.ShouldBe(1);
        output.ToString().ShouldBeEmpty();
        error.ToString().ShouldContain(
            "The caller-owned user systemd slice and its finite memory and swap limits could not be verified.",
            StringComparison.Ordinal);
        error.ToString().ShouldNotContain("synthetic user manager error", StringComparison.Ordinal);
        runner.Runs.ShouldBeEmpty();
        context.Operations.ShouldBeEmpty();
        context.Roots.ShouldBeEmpty();
        unitQueries.Count.ShouldBeLessThanOrEqualTo(1);
        unitQueries.ShouldAllSatisfy(query => query.ShouldBe(["--user", "show", "--property=Id,LoadState,ActiveState,ControlGroup", "--", slice]));
        var actualLogPaths = Directory.GetFiles(logs.Path).Order(StringComparer.Ordinal).ToArray();
        actualLogPaths.ShouldBe(expectedLogPaths);
        actualLogPaths.ShouldAllSatisfy(path => new FileInfo(path).Length.ShouldBe(0L));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public async Task Child_arguments_outputs_and_exit_code_survive_verified_cleanup(int childExitCode)
    {
        // Arrange
        using var context = new IsolatedPodmanTestContext();
        var runner = ScriptedProcessRunner.Returning(new ProcessOutput(childExitCode, "child output\n", "child error\n"));

        // Act
        var result = await context.RunCommand(runner, ["--image-store", "literal value", "--cgroup-parent-slice", "literal.slice", "", "--", "@literal"]);

        // Assert
        result.ExitCode.ShouldBe(childExitCode);
        result.Output.ShouldBe("child output\n");
        result.Error.ShouldBe("child error\n");
        var run = runner.Runs.ShouldHaveSingleItem();
        run.StartInfo.FileName.ShouldBe(context.Executable);
        run.StartInfo.WorkingDirectory.ShouldBe(context.ImageStore);
        run.StartInfo.ArgumentList.ShouldBe(["--image-store", "literal value", "--cgroup-parent-slice", "literal.slice", "", "--", "@literal"]);
        Directory.Exists(context.Roots.ShouldHaveSingleItem()).ShouldBeFalse();
        context.Operations.ShouldContain("pod rm --all --force --time 0");
        context.Operations.ShouldContain("ps --all --quiet");
        context.Operations.Count(operation => string.Equals(operation, "ps --quiet", StringComparison.Ordinal)).ShouldBeGreaterThanOrEqualTo(2);
    }

    [Theory(SkipUnless = nameof(PosixTestSupport.CanRestrictPermissions), SkipType = typeof(PosixTestSupport), Skip = "Needs POSIX permissions that bind the current user.")]
    [UnsupportedOSPlatform("windows")]
    [InlineData(0)]
    [InlineData(17)]
    public async Task Verified_cleanup_reclaims_a_private_read_only_image_layer(int childExitCode)
    {
        // Arrange
        using var context = new IsolatedPodmanTestContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var runner = ScriptedProcessRunner.Answering(run =>
        {
            context.StagePrivateImageLayer(run.StartInfo);
            return new ProcessOutput(childExitCode, string.Empty);
        });
        var sharedImage = Path.Combine(context.ImageStore, "shared-image");
        await File.WriteAllTextAsync(sharedImage, "shared image bytes", cancellationToken);

        // Act
        var result = await context.RunCommand(runner);

        // Assert
        result.Error.ShouldBeEmpty();
        result.ExitCode.ShouldBe(childExitCode);
        Directory.Exists(context.Roots.ShouldHaveSingleItem()).ShouldBeFalse();
        var sharedImageContents = await File.ReadAllTextAsync(sharedImage, cancellationToken);
        sharedImageContents.ShouldBe("shared image bytes");
    }

    [Fact]
    public async Task Engine_overrides_stay_in_the_child_and_preserve_the_session_bus()
    {
        // Arrange
        using var context = new IsolatedPodmanTestContext();
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();
        var child = new ProcessStartInfo(context.Executable);
        child.Environment["DOCKER_HOST"] = "unix:/shared/docker.sock";
        child.Environment["CONTAINER_HOST"] = "unix:/shared/podman.sock";
        child.Environment["CONTAINERS_CONF_OVERRIDE"] = "/shared/override.conf";
        child.Environment["GH_TOKEN"] = "test-token";
        child.Environment["XDG_RUNTIME_DIR"] = "/run/user/1000";
        child.Environment["DBUS_SESSION_BUS_ADDRESS"] = "unix:path=/run/user/1000/bus";
        var parentStorage = Environment.GetEnvironmentVariable("CONTAINERS_STORAGE_CONF");
        var runner = ScriptedProcessRunner.Answering(run =>
        {
            ReferenceEquals(run.StartInfo, child).ShouldBeFalse();
            var storage = File.ReadAllText(run.StartInfo.Environment["CONTAINERS_STORAGE_CONF"]!);
            var engine = File.ReadAllText(run.StartInfo.Environment["CONTAINERS_CONF"]!);
            storage.ShouldContain("additionalimagestores = [\"", StringComparison.Ordinal);
            engine.ShouldContain("lock_type = \"file\"", StringComparison.Ordinal);
            var environment = run.StartInfo.Environment;
            environment.ContainsKey("DOCKER_HOST").ShouldBeFalse();
            environment.ContainsKey("CONTAINER_HOST").ShouldBeFalse();
            environment.ContainsKey("CONTAINERS_CONF_OVERRIDE").ShouldBeFalse();
            environment.ContainsKey("GH_TOKEN").ShouldBeFalse();
            environment["XDG_RUNTIME_DIR"].ShouldBe("/run/user/1000");
            environment["DBUS_SESSION_BUS_ADDRESS"].ShouldBe("unix:path=/run/user/1000/bus");
            environment["ASPIRE_CONTAINER_RUNTIME"].ShouldBe("podman");
            return new ProcessOutput(0, string.Empty);
        });

        // Act
        var exitCode = await IsolatedPodman.Run(context.Host, context.ImageStore, "slirp4netns", child, runner, output, error, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        Environment.GetEnvironmentVariable("CONTAINERS_STORAGE_CONF").ShouldBe(parentStorage);
        child.Environment["DOCKER_HOST"].ShouldBe("unix:/shared/docker.sock");
        child.Environment["CONTAINER_HOST"].ShouldBe("unix:/shared/podman.sock");
        child.Environment["GH_TOKEN"].ShouldBe("test-token");
        Directory.Exists(context.ImageStore).ShouldBeTrue();
    }

    [Fact]
    public async Task Each_run_creates_a_new_private_scope()
    {
        // Arrange
        using var context = new IsolatedPodmanTestContext();
        var runner = ScriptedProcessRunner.Returning(new ProcessOutput(0, string.Empty));

        // Act
        var first = await context.RunCommand(runner);
        var second = await context.RunCommand(runner);

        // Assert
        first.ExitCode.ShouldBe(0);
        second.ExitCode.ShouldBe(0);
        context.Roots.Count.ShouldBe(2);
        context.Roots[0].ShouldNotBe(context.Roots[1]);
        context.Roots.Any(Directory.Exists).ShouldBeFalse();
    }

    [Fact]
    public async Task Runtime_parent_keeps_transient_paths_private_and_leaves_graph_and_child_temp_on_disk()
    {
        // Arrange
        using var context = new IsolatedPodmanTestContext();
        using var runtimeParent = new TemporaryDirectory();
        var ownedMarker = Path.Combine(runtimeParent.Path, "caller-owned");
        await File.WriteAllTextAsync(ownedMarker, "caller-owned data", TestContext.Current.CancellationToken);
        var childTemp = Environment.GetEnvironmentVariable("TMPDIR");
        var runner = ScriptedProcessRunner.Answering(run =>
        {
            var runRoot = context.RunRoots[^1];
            var runtimeRoot = Path.GetDirectoryName(runRoot)!;
            var storageRoot = context.Roots[^1];
            var engine = File.ReadAllText(run.StartInfo.Environment["CONTAINERS_CONF"]!);
            Path.GetDirectoryName(runtimeRoot).ShouldBe(runtimeParent.Path);
            Path.GetDirectoryName(storageRoot).ShouldBe(Path.TrimEndingDirectorySeparator(Path.GetTempPath()));
            runtimeRoot.ShouldNotBe(storageRoot);
            Directory.Exists(Path.Combine(storageRoot, "graph", "volumes")).ShouldBeTrue();
            Directory.Exists(Path.Combine(storageRoot, "graph", "libpod")).ShouldBeTrue();
            Directory.Exists(Path.Combine(runtimeRoot, "libpod")).ShouldBeTrue();
            Directory.Exists(Path.Combine(runtimeRoot, "networks")).ShouldBeTrue();
            engine.ShouldContain("network_config_dir", StringComparison.Ordinal);
            run.StartInfo.Environment.TryGetValue("TMPDIR", out var temporary).ShouldBe(childTemp is not null);
            temporary.ShouldBe(childTemp);
            return new ProcessOutput(0, string.Empty);
        });

        // Act
        var first = await context.RunCommand(runner, runtimeParent: runtimeParent.Path);
        var second = await context.RunCommand(runner, runtimeParent: runtimeParent.Path);

        // Assert
        first.ExitCode.ShouldBe(0, first.Error);
        second.ExitCode.ShouldBe(0, second.Error);
        context.RunRoots.Count.ShouldBe(2);
        context.RunRoots[0].ShouldNotBe(context.RunRoots[1]);
        context.RunRoots.Any(Directory.Exists).ShouldBeFalse();
        context.Roots.Any(Directory.Exists).ShouldBeFalse();
        Directory.GetFileSystemEntries(runtimeParent.Path).ShouldBe([ownedMarker]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_replaced_runtime_directory_retains_both_private_scopes(bool replaceDirectory)
    {
        // Arrange
        using var context = new IsolatedPodmanTestContext();
        using var runtimeParent = new TemporaryDirectory();
        var runner = ScriptedProcessRunner.Answering(_ =>
        {
            var runRoot = context.RunRoots.ShouldHaveSingleItem();
            Directory.Delete(runRoot);
            if (replaceDirectory)
            {
                File.WriteAllText(runRoot, "replaced runtime directory");
            }
            return new ProcessOutput(0, string.Empty);
        });

        // Act
        var result = await context.RunCommand(runner, runtimeParent: runtimeParent.Path);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("cleanup failed", StringComparison.Ordinal);
        result.Error.ShouldContain(runtimeParent.Path, StringComparison.Ordinal);
        Directory.Exists(context.Roots.ShouldHaveSingleItem()).ShouldBeTrue();
        var runRoot = context.RunRoots.ShouldHaveSingleItem();
        Directory.Exists(Path.GetDirectoryName(runRoot)).ShouldBeTrue();
        File.Exists(runRoot).ShouldBe(replaceDirectory);
        context.Operations.ShouldNotContain("pod rm --all --force --time 0");
    }

    [Fact]
    public async Task Missing_runtime_parent_is_rejected_before_probing_the_engine()
    {
        // Arrange
        using var context = new IsolatedPodmanTestContext();
        var runtimeParent = Path.Combine(context.ImageStore, "missing-runtime-parent");
        var runner = ScriptedProcessRunner.Returning(new ProcessOutput(0, string.Empty));

        // Act
        var result = await context.RunCommand(runner, runtimeParent: runtimeParent);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("The runtime parent must be an existing absolute directory", StringComparison.Ordinal);
        context.Operations.ShouldBeEmpty();
        Directory.Exists(runtimeParent).ShouldBeFalse();
    }

    [Fact]
    public async Task Unverified_scope_skips_the_child_and_all_removal_operations()
    {
        // Arrange
        using var context = new IsolatedPodmanTestContext { InvalidScope = true };
        var runner = ScriptedProcessRunner.Returning(new ProcessOutput(0, string.Empty));

        // Act
        var result = await context.RunCommand(runner);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("did not report the expected local rootless engine", StringComparison.Ordinal);
        result.Error.ShouldContain("retained private engine files", StringComparison.Ordinal);
        runner.Runs.Count.ShouldBe(0);
        context.Operations.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Replaced_configuration_cannot_redirect_cleanup()
    {
        // Arrange
        using var context = new IsolatedPodmanTestContext();
        var runner = ScriptedProcessRunner.Answering(run =>
        {
            File.WriteAllText(run.StartInfo.Environment["CONTAINERS_STORAGE_CONF"]!, "changed");
            return new ProcessOutput(0, string.Empty);
        });

        // Act
        var result = await context.RunCommand(runner);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("configuration file changed", StringComparison.Ordinal);
        Directory.Exists(context.Roots.ShouldHaveSingleItem()).ShouldBeTrue();
        context.Operations.ShouldNotContain("pod rm --all --force --time 0");
    }

    [Fact]
    public async Task Cancellation_still_cleans_the_verified_engine()
    {
        // Arrange
        using var context = new IsolatedPodmanTestContext();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var child = new ProcessStartInfo(context.Executable);
        var runner = new ScriptedProcessRunner(async (_, _, ct) =>
        {
            await cancellation.CancelAsync();
            return await Task.FromCanceled<ProcessOutput>(ct);
        });

        // Act
        Func<Task> run = () => IsolatedPodman.Run(
            context.Host, context.ImageStore, "slirp4netns", child, runner, TextWriter.Null, TextWriter.Null, cancellation.Token);

        // Assert
        await run.ShouldThrow<TaskCanceledException>();
        Directory.Exists(context.Roots.ShouldHaveSingleItem()).ShouldBeFalse();
        context.Operations.ShouldContain("pod rm --all --force --time 0");
    }

    [Fact]
    public async Task Deadline_remains_primary_while_verified_cleanup_retries_a_transient_container_removal()
    {
        // Arrange
        const string removal = "rm --all --force --time 0";
        using var context = new IsolatedPodmanTestContext { FailOperation = removal };
        using var runtimeParent = new TemporaryDirectory();
        using var error = new InvariantStringWriter();
        var timeout = new TimeoutException("The fixture child exceeded its deadline.");
        var runner = ScriptedProcessRunner.Throwing(timeout);
        List<CancellationToken> removalTokens = [];
        var host = context.Host with
        {
            RunProcess = async (startInfo, ct) =>
            {
                if (string.Equals(startInfo.ArgumentList[1], "rm", StringComparison.Ordinal))
                {
                    removalTokens.Add(ct);
                }

                var result = await context.Host.RunProcess(startInfo, ct);
                if (result.ExitCode != 0)
                {
                    context.FailOperation = null;
                }

                return result;
            },
        };
        var child = new ProcessStartInfo(context.Executable);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        Func<Task> run = async () => _ = await IsolatedPodman.Run(host, context.ImageStore, "pasta", runtimeParent.Path,
            child, runner, TextWriter.Null, error, cancellationToken);
        var failure = await run.ShouldThrow<TimeoutException>();

        // Assert
        ReferenceEquals(failure, timeout).ShouldBeTrue();
        removalTokens.Count.ShouldBe(2, "A transient failure should get one retry.");
        removalTokens.Distinct().ShouldHaveSingleItem().CanBeCanceled.ShouldBeTrue();
        var firstRemoval = context.Operations.IndexOf(removal);
        var retry = context.Operations.LastIndexOf(removal);
        var verifiedAgain = context.Operations.Skip(firstRemoval + 1).Take(retry - firstRemoval - 1)
            .Any(operation => operation.StartsWith("info --format {{.Host.ServiceIsRemote}}\n", StringComparison.Ordinal));
        verifiedAgain.ShouldBeTrue("Reverify the exact private engine identity before retrying cleanup.");
        Directory.Exists(context.Roots.ShouldHaveSingleItem()).ShouldBeFalse();
        Directory.Exists(Path.GetDirectoryName(context.RunRoots.ShouldHaveSingleItem())).ShouldBeFalse();
        Directory.Exists(runtimeParent.Path).ShouldBeTrue();
        error.ToString().ShouldNotContain("raw engine output", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("nonzero", false, 2, "could not complete the 'rm' operation")]
    [InlineData("nonzero", true, 1, "did not report the expected local rootless engine")]
    [InlineData("overrun", false, 1, "could not complete the 'rm' operation")]
    [InlineData("denied", false, 1, "Private removal was denied.")]
    public async Task The_deadline_stays_primary_when_cleanup_cannot_safely_finish(
        string failureKind, bool changedIdentity, int expectedRemovals, string diagnostic)
    {
        // Arrange
        const string removal = "rm --all --force --time 0";
        using var context = new IsolatedPodmanTestContext { FailOperation = removal };
        using var runtimeParent = new TemporaryDirectory();
        using var error = new InvariantStringWriter();
        var timeout = new TimeoutException("The fixture child exceeded its deadline.");
        var runner = ScriptedProcessRunner.Throwing(timeout);
        var host = context.Host with
        {
            RunProcess = async (startInfo, ct) =>
            {
                var result = await context.Host.RunProcess(startInfo, ct);
                if (result.ExitCode == 0)
                {
                    return result;
                }

                context.InvalidScope = changedIdentity;
                return failureKind switch
                {
                    "overrun" => result with { StandardError = new string('x', 1_048_577) },
                    "denied" => throw new UnauthorizedAccessException("Private removal was denied."),
                    _ => result,
                };
            },
        };
        var child = new ProcessStartInfo(context.Executable);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        Func<Task> run = async () => _ = await IsolatedPodman.Run(host, context.ImageStore, "pasta", runtimeParent.Path,
            child, runner, TextWriter.Null, error, cancellationToken);
        var thrown = await run.ShouldThrow<TimeoutException>();

        // Assert
        ReferenceEquals(thrown, timeout).ShouldBeTrue();
        var removals = context.Operations.Count(operation => operation == removal);
        removals.ShouldBe(expectedRemovals);
        context.Operations.ShouldNotContain("network prune --force");
        Directory.Exists(context.Roots.ShouldHaveSingleItem()).ShouldBeTrue();
        Directory.Exists(Path.GetDirectoryName(context.RunRoots.ShouldHaveSingleItem())).ShouldBeTrue();
        var errorText = error.ToString();
        errorText.ShouldContain(diagnostic, StringComparison.Ordinal);
        errorText.ShouldContain("retained private engine files", StringComparison.Ordinal);
        errorText.ShouldContain("retained private runtime files", StringComparison.Ordinal);
        errorText.ShouldNotContain("raw engine output", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("rm --all --force --time 0", 0, 1)]
    [InlineData("network prune --force", 0, 1)]
    [InlineData("network prune --force", 17, 17)]
    [InlineData("image rm --all", 0, 1)]
    [InlineData("image rm --all", 17, 17)]
    public async Task Failed_cleanup_retains_the_engine_and_bounds_its_diagnostic(string failedOperation, int childExitCode, int expectedExitCode)
    {
        // Arrange
        using var context = new IsolatedPodmanTestContext { FailOperation = failedOperation };
        var runner = ScriptedProcessRunner.Returning(new ProcessOutput(childExitCode, string.Empty));

        // Act
        var result = await context.RunCommand(runner);

        // Assert
        result.ExitCode.ShouldBe(expectedExitCode);
        result.Error.ShouldContain("cleanup failed", StringComparison.Ordinal);
        result.Error.ShouldNotContain("raw engine output", StringComparison.Ordinal);
        Directory.Exists(context.Roots.ShouldHaveSingleItem()).ShouldBeTrue();
    }
}
