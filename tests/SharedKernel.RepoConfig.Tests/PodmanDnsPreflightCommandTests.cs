using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace SharedKernel.RepoConfig.Tests;

[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class PodmanDnsPreflightCommandTests
{
    [Fact]
    public async Task Tool_help_lists_the_preflight_command()
    {
        // Arrange
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolApplication.Run(
            ["--help"],
            output,
            error,
            Environment.CurrentDirectory,
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        output.ToString().ShouldContain("podman-dns-preflight", StringComparison.Ordinal);
        error.ToString().ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Command_help_uses_the_public_preflight_entry_point()
    {
        // Arrange
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolApplication.Run(
            ["podman-dns-preflight", "--help"],
            output,
            error,
            Environment.CurrentDirectory,
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        output.ToString().ShouldContain("Usage: sharedkernel-repo podman-dns-preflight", StringComparison.Ordinal);
        error.ToString().ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Help_is_recognized_with_other_valid_options()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();

        // Act
        var result = await context.Run(["--check", "--help"]);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldContain("Usage: sharedkernel-repo podman-dns-preflight", StringComparison.Ordinal);
        result.StandardError.ShouldBe(string.Empty);
        context.Commands.ShouldBe([]);
    }

    [Fact]
    public async Task Missing_podman_is_inapplicable()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();

        // Act
        var result = await context.Run(podmanExecutable: null);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldContain("podman is not installed", StringComparison.Ordinal);
        result.StandardError.ShouldBe(string.Empty);
        context.Commands.ShouldBe([]);
    }

    [Fact]
    public async Task Relative_podman_executable_fails_closed()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();

        // Act
        var result = await context.Run(podmanExecutable: "podman");

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("executable path is not absolute", StringComparison.Ordinal);
        context.Commands.ShouldBe([]);
    }

    [Fact]
    public async Task Non_linux_host_fails_closed()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        var executable = Path.Combine(context.RootPath, "podman");

        // Act
        var result = await context.Run(podmanExecutable: executable, isLinux: false);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("only on Linux", StringComparison.Ordinal);
        context.Commands.ShouldBe([]);
    }

    [Theory]
    [InlineData("info --format {{.Host.ServiceIsRemote}}", "unknown\n", "invalid remote-service mode")]
    [InlineData("info --format {{.Host.Security.Rootless}}", "unknown\n", "invalid rootless mode")]
    public async Task Invalid_podman_mode_metadata_fails_closed(string query, string response, string expectedMessage)
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        context.SetResponse(query, response);

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain(expectedMessage, StringComparison.Ordinal);
        result.StandardOutput.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Rootful_podman_is_inapplicable_and_quiet()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        context.SetResponse("info --format {{.Host.Security.Rootless}}", "false\n");

        // Act
        var result = await context.Run(["--quiet"]);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldBe(string.Empty);
        result.StandardError.ShouldBe(string.Empty);
        context.Commands.ShouldBe(
        [
            "--remote=false info --format {{.Host.ServiceIsRemote}}",
            "--remote=false info --format {{.Host.Security.Rootless}}"
        ]);
    }

    [Fact]
    public async Task Relative_reported_run_root_fails_closed()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        context.SetResponse("info --format {{.Store.RunRoot}}", "relative-runroot\n");

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("invalid run root", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Filesystem_root_reported_as_run_root_fails_closed()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        var filesystemRoot = Path.GetPathRoot(context.RunRoot) ?? Path.DirectorySeparatorChar.ToString();
        context.SetResponse("info --format {{.Store.RunRoot}}", filesystemRoot + Environment.NewLine);

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("unsafe run root", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_reported_run_root_fails_closed()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        var missingRunRoot = Path.Combine(context.RootPath, "missing-runroot");
        context.SetResponse("info --format {{.Store.RunRoot}}", missingRunRoot + Environment.NewLine);

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("run root does not exist", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Known_internal_network_config_is_healthy_and_read_only()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext
        {
            Networks = "trip\n",
            Containers = "container-id\n"
        };
        context.WriteConfig("trip");
        context.WriteConfig("trip%int");
        context.AddDaemon(1201);
        var before = context.Snapshot();

        // Act
        var result = await context.Run();
        var after = context.Snapshot();

        // Assert
        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldContain("1 container(s) running", StringComparison.Ordinal);
        result.StandardError.ShouldBe(string.Empty);
        after.ShouldBe(before);
        context.Commands.ShouldBe(
        [
            "--remote=false info --format {{.Host.ServiceIsRemote}}",
            "--remote=false info --format {{.Host.Security.Rootless}}",
            "--remote=false info --format {{.Store.RunRoot}}",
            "--remote=false ps --quiet",
            "--remote=false network ls --format {{.Name}}",
            "--remote=false ps --quiet",
            "--remote=false network ls --format {{.Name}}",
            "--remote=false info --format {{.Store.RunRoot}}"
        ]);
    }

    [Fact]
    public async Task Check_mode_reports_orphan_config_without_mutation()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext { Networks = "trip\n" };
        context.WriteConfig("trip%int");
        context.WriteConfig("trip-old%int");
        context.AddDaemon(1202);
        var before = context.Snapshot();

        // Act
        var result = await context.Run(["--check"]);
        var after = context.Snapshot();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("trip-old%int", StringComparison.Ordinal);
        result.StandardError.ShouldNotContain("networks Podman no longer has: trip%int", StringComparison.Ordinal);
        after.ShouldBe(before);
    }

    [Fact]
    public async Task Live_runroot_daemon_without_pid_file_fails_closed()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        context.AddDaemon(1203, writePidFile: false);
        var before = context.Snapshot();

        // Act
        var result = await context.Run();
        var after = context.Snapshot();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("live aardvark-dns", StringComparison.Ordinal);
        after.ShouldBe(before);
    }

    [Fact]
    public async Task Missing_refcount_fails_without_mutation()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        context.AddDaemon(1204, refCount: null);
        var before = context.Snapshot();

        // Act
        var result = await context.Run();
        var after = context.Snapshot();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("ref-count does not exist", StringComparison.Ordinal);
        after.ShouldBe(before);
    }

    [Fact]
    public async Task Daemon_in_different_network_namespace_is_stale()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        context.AddDaemon(1210);
        context.SetProcessNamespaceIdentity(1210, "1:200");
        var before = context.Snapshot();

        // Act
        var result = await context.Run();
        var after = context.Snapshot();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("different network namespace", StringComparison.Ordinal);
        after.ShouldBe(before);
    }

    [Fact]
    public async Task Missing_daemon_namespace_identity_fails_closed()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        context.AddDaemon(1211);
        context.DeleteProcessNamespaceIdentity(1211);

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("unable to verify safely", StringComparison.Ordinal);
        result.StandardError.ShouldContain("network namespace identities", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Live_daemon_without_active_namespace_is_stale()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        context.AddDaemon(1214);
        context.DeleteActiveNamespace();
        var before = context.Snapshot();

        // Act
        var result = await context.Run();
        var after = context.Snapshot();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("rootless-netns", StringComparison.Ordinal);
        result.StandardError.ShouldContain("does not exist", StringComparison.Ordinal);
        after.ShouldBe(before);
    }

    [Fact]
    public async Task Live_daemon_without_stat_fails_closed()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        context.AddDaemon(1215);

        // Act
        var result = await context.Run(statExecutable: null);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("GNU stat is required", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Multiple_live_daemons_are_ambiguous()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        context.AddDaemon(1216);
        context.AddDaemon(1217);
        var before = context.Snapshot();

        // Act
        var result = await context.Run();
        var after = context.Snapshot();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("multiple aardvark-dns processes", StringComparison.Ordinal);
        result.StandardError.ShouldContain("1216 1217", StringComparison.Ordinal);
        after.ShouldBe(before);
    }

    [Fact]
    public async Task Reused_daemon_pid_during_inspection_fails_closed()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        context.AddDaemon(1212, startTime: 100);
        context.ChangeProcessStartTimeOnRecheck(1212, 200);

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("process identity changed during inspection", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Late_namespace_replacement_fails_closed()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        context.AddDaemon(1213);
        context.ChangeNamespaceIdentityOnRecheck(1213, "1:200");

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("network namespace identity changed during inspection", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pid_reuse_during_final_namespace_check_fails_closed()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        context.AddDaemon(1218, startTime: 100);
        context.ChangeProcessStartTimeOnFinalNamespaceCheck(1218, 200);

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("process identity changed during inspection", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0", "reference count is 0")]
    [InlineData("invalid", "unable to verify safely")]
    public async Task Invalid_refcount_fails_closed(string refCount, string expectedMessage)
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        context.AddDaemon(1205, refCount: refCount);
        var before = context.Snapshot();

        // Act
        var result = await context.Run();
        var after = context.Snapshot();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain(expectedMessage, StringComparison.Ordinal);
        after.ShouldBe(before);
    }

    [Fact]
    public async Task Same_user_daemon_for_another_run_root_is_ignored()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        var otherConfig = Path.Combine(context.RootPath, "other-runroot", "networks", "aardvark-dns");
        context.AddDaemon(1206, otherConfig, writePidFile: false, refCount: null);

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(0);
        result.StandardError.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Equals_form_config_argument_matches_exact_run_root()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        context.AddDaemon(1207, equalsForm: true);

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(0);
        result.StandardError.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Config_without_live_daemon_fails_closed()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext { Networks = "trip\n" };
        context.WriteConfig("trip");

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("does not identify a live aardvark-dns daemon", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Malformed_aardvark_directory_path_fails_closed()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        context.WriteAardvarkDirectoryAsFile();

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("unable to verify safely", StringComparison.Ordinal);
        result.StandardOutput.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Malformed_networks_directory_path_fails_closed()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        context.WriteNetworksDirectoryAsFile();

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("unable to verify safely", StringComparison.Ordinal);
        result.StandardOutput.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Malformed_pid_file_fails_closed()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        context.WriteConfig("aardvark.pid", "not-a-pid\n");

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("contains an invalid PID", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pid_file_for_exited_process_is_rejected()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        context.WriteConfig("aardvark.pid", "1401\n");

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("PID 1401, which is not running", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pid_file_for_non_aardvark_process_is_rejected()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        var otherConfig = Path.Combine(context.RootPath, "other-runroot", "networks", "aardvark-dns");
        context.AddDaemon(1402, otherConfig);
        context.SetProcessName(1402, "conmon");

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("PID 1402, which is conmon", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pid_file_for_aardvark_using_another_run_root_is_rejected()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        var otherConfig = Path.Combine(context.RootPath, "other-runroot", "networks", "aardvark-dns");
        context.AddDaemon(1403, otherConfig);

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("not configured for", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Running_container_state_change_during_inspection_fails_closed()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        context.SetResponses("ps --quiet", "one\n", "two\n");

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("running container state changed", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pid_file_change_during_inspection_fails_closed()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        context.AddDaemon(1501);
        context.ChangePidFileOnRecheck("9999\n");

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("PID file changed during inspection", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reference_count_change_during_inspection_fails_closed()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        context.AddDaemon(1502);
        context.ChangeRefCountOnRecheck("2\n");

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("reference count changed during inspection", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Aardvark_configuration_change_during_inspection_fails_closed()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        context.AddDaemon(1503);
        context.ChangeConfigOnRecheck("trip");

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("configuration changed during inspection", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Podman_network_state_change_during_inspection_fails_closed()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        context.AddDaemon(1504);
        context.WriteConfig("trip");
        context.SetResponses("network ls --format {{.Name}}", "trip\n", "other\n");

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("Podman network state changed", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Podman_run_root_change_during_inspection_fails_closed()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        var otherRunRoot = Path.Combine(context.RootPath, "other-runroot");
        context.SetResponses(
            "info --format {{.Store.RunRoot}}",
            context.RunRoot + Environment.NewLine,
            otherRunRoot + Environment.NewLine);

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("Podman run root changed", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_root_directory_state_change_during_inspection_fails_closed()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        context.CreateNetworksDirectoryOnRecheck();

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("run-root directory state changed", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("false\ntrue\n")]
    [InlineData("")]
    public async Task Invalid_single_value_output_fails_closed(string output)
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        context.SetResponse("info --format {{.Host.ServiceIsRemote}}", output);

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("remote-service mode", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Oversized_podman_output_fails_closed_without_echoing_it()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        var oversizedOutput = new string('x', 1_048_577);
        context.SetResponse("info --format {{.Host.ServiceIsRemote}}", oversizedOutput);

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe(
            $"podman dns preflight: unable to verify safely: podman info failed while reading remote-service mode{Environment.NewLine}");
    }

    [Fact]
    public async Task Oversized_podman_error_fails_closed_without_echoing_it()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        var oversizedError = new string('x', 1_048_577);
        context.SetResponse("info --format {{.Host.ServiceIsRemote}}", "false\n", oversizedError);

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe(
            $"podman dns preflight: unable to verify safely: podman info failed while reading remote-service mode{Environment.NewLine}");
    }

    [Fact]
    public async Task Process_start_failure_names_the_executable()
    {
        // Arrange
        var executable = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing-command");
        ProcessStartInfo startInfo = new(executable)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        Func<Task> run = () => PodmanDnsPreflightCommand.RunProcess(startInfo, TestContext.Current.CancellationToken);

        // Act
        var exception = await run.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain(executable, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stat_start_failure_names_the_executable_in_command_diagnostics()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        var statExecutable = Path.Combine(context.RootPath, "missing-stat");
        context.AddDaemon(1220);
        context.UseSystemProcessFor(statExecutable);

        // Act
        var result = await context.Run(statExecutable: statExecutable);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain(statExecutable, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Process_cleanup_ignores_an_already_unavailable_process()
    {
        // Arrange
        using Process process = new();
        var completed = false;

        // Act
        await PodmanDnsPreflightCommand.Stop(process);
        completed = true;

        // Assert
        completed.ShouldBe(true);
    }

    [Fact]
    public async Task Process_output_capture_retains_only_the_limit_marker()
    {
        // Arrange
        var oversizedOutput = new string('x', 1_048_577) + "discarded";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(oversizedOutput));
        using var reader = new StreamReader(stream, Encoding.UTF8);

        // Act
        var capturedOutput = await PodmanDnsPreflightCommand.ReadBoundedOutput(
            reader,
            TestContext.Current.CancellationToken);

        // Assert
        capturedOutput.Length.ShouldBe(1_048_577);
    }

    [Theory]
    [InlineData("info --format {{.Host.ServiceIsRemote}}", "remote-service mode")]
    [InlineData("info --format {{.Host.Security.Rootless}}", "rootless mode")]
    [InlineData("info --format {{.Store.RunRoot}}", "run root")]
    [InlineData("ps --quiet", "running containers")]
    [InlineData("network ls --format {{.Name}}", "aardvark-dns configuration")]
    public async Task Podman_query_failure_fails_closed(string query, string expectedMessage)
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        context.WriteConfig("trip");
        context.SetFailure(query);

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain(expectedMessage, StringComparison.Ordinal);
        result.StandardOutput.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Remote_podman_fails_closed()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();
        context.SetResponse("info --format {{.Host.ServiceIsRemote}}", "true\n");

        // Act
        var result = await context.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("remote Podman state", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_argument_is_a_usage_error()
    {
        // Arrange
        using var context = new PodmanDnsPreflightTestContext();

        // Act
        var result = await context.Run(["--repair"]);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.StandardError.ShouldContain("Unknown argument: --repair", StringComparison.Ordinal);
        context.Commands.ShouldBe([]);
    }
}
