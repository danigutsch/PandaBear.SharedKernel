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

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public async Task Child_arguments_outputs_and_exit_code_survive_verified_cleanup(int childExitCode)
    {
        // Arrange
        using var context = new IsolatedPodmanTestContext();
        var runner = ScriptedProcessRunner.Returning(new ProcessOutput(childExitCode, "child output\n", "child error\n"));

        // Act
        var result = await context.RunCommand(runner, ["--image-store", "literal value", "", "--", "@literal"]);

        // Assert
        result.ExitCode.ShouldBe(childExitCode);
        result.Output.ShouldBe("child output\n");
        result.Error.ShouldBe("child error\n");
        var run = runner.Runs.ShouldHaveSingleItem();
        run.StartInfo.FileName.ShouldBe(context.Executable);
        run.StartInfo.WorkingDirectory.ShouldBe(context.ImageStore);
        run.StartInfo.ArgumentList.ShouldBe(["--image-store", "literal value", "", "--", "@literal"]);
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

    [Theory]
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
