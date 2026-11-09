using static PandaBear.Sandbox.Tool.Tests.SandboxToolHarness;

namespace PandaBear.Sandbox.Tool.Tests;

/// <summary>Tests the <c>doctor</c> command.</summary>
[Trait("Capability", "Sandbox")]
[Collection(EnvironmentCollection.Name)]
public sealed class DoctorCommandTests
{
    [Fact]
    public async Task A_permissive_environment_reports_every_probe_allowed_and_exits_zero()
    {
        // Arrange
        using var repository = CreateRepository(null);

        // Act
        var (exitCode, output, error) = await Run(repository.Path, new FakeSandboxEnvironment());

        // Assert
        exitCode.ShouldBe(0);
        error.ShouldBe(string.Empty);
        output.ShouldContain("allowed unix-socket: socket ok", StringComparison.Ordinal);
        output.ShouldContain("allowed named-pipe: pipe ok", StringComparison.Ordinal);
        output.ShouldContain("allowed temp-write: write ok", StringComparison.Ordinal);
        output.ShouldContain("allowed nuget-write: write ok", StringComparison.Ordinal);
        output.ShouldContain("4 probes: 4 good, 0 not good, 0 skipped.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Denied_sockets_are_explained_with_the_dotnet_workaround_but_do_not_fail_the_run()
    {
        // Arrange
        using var repository = CreateRepository(null);
        var environment = new FakeSandboxEnvironment { Socket = (ProbeOutcome.Denied, "refused"), Pipe = (ProbeOutcome.Denied, "refused") };

        // Act
        var (exitCode, output, _) = await Run(repository.Path, environment);

        // Assert
        exitCode.ShouldBe(0, "Sockets are advisory: a sandbox may deny them on purpose.");
        output.ShouldContain("denied unix-socket: refused", StringComparison.Ordinal);
        output.ShouldContain("-> Project-mode 'dotnet format' and 'dotnet test' cannot start", StringComparison.Ordinal);
        output.ShouldContain("dotnet format whitespace --folder", StringComparison.Ordinal);
        output.ShouldContain("run the built test executable directly", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/tmp/x", "temp-write")]
    [InlineData("/home/user/.nuget/packages", "nuget-write")]
    public async Task A_required_folder_that_cannot_be_written_fails_the_run_and_says_what_to_allow(string directory, string id)
    {
        // Arrange
        using var repository = CreateRepository(null);
        var environment = new FakeSandboxEnvironment();
        environment.Writes[directory] = (ProbeOutcome.Denied, "refused");

        // Act
        var (exitCode, output, _) = await Run(repository.Path, environment);

        // Assert
        exitCode.ShouldBe(1);
        output.ShouldContain($"denied {id}: refused", StringComparison.Ordinal);
        output.ShouldContain("allow write access", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_nuget_folder_is_skipped_and_does_not_fail_the_run()
    {
        // Arrange
        using var repository = CreateRepository(null);
        var environment = new FakeSandboxEnvironment { NuGetPackagesDirectory = null };

        // Act
        var (exitCode, output, _) = await Run(repository.Path, environment);

        // Assert
        exitCode.ShouldBe(0);
        output.ShouldContain("skipped nuget-write:", StringComparison.Ordinal);
        environment.Calls.ShouldNotContain(static call => call.Contains(".nuget", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_configured_path_that_can_be_read_is_exposed_and_fails_the_run()
    {
        // Arrange
        using var repository = CreateRepository("""{"version":1,"deniedReadPaths":["~/.secrets","/etc/shadow"]}""");
        var environment = new FakeSandboxEnvironment();
        environment.Reads["/home/user/.secrets"] = (ProbeOutcome.Allowed, "content readable");
        environment.Reads["/etc/shadow"] = (ProbeOutcome.Denied, "refused");

        // Act
        var (exitCode, output, _) = await Run(repository.Path, environment);

        // Assert
        exitCode.ShouldBe(1);
        output.ShouldContain("exposed denied-read ~/.secrets: content readable", StringComparison.Ordinal);
        output.ShouldContain("blocked denied-read /etc/shadow: refused", StringComparison.Ordinal);
        output.ShouldContain("add it to the denied reads", StringComparison.Ordinal);
        environment.Calls.ShouldContain("read /home/user/.secrets", "A leading ~/ is expanded to the home directory.");
    }

    [Fact]
    public async Task A_blocked_path_alone_is_good_and_the_exit_code_is_zero()
    {
        // Arrange
        using var repository = CreateRepository("""{"version":1,"deniedReadPaths":["/etc/shadow"]}""");
        var environment = new FakeSandboxEnvironment();
        environment.Reads["/etc/shadow"] = (ProbeOutcome.Denied, "refused");

        // Act
        var (exitCode, output, _) = await Run(repository.Path, environment);

        // Assert
        exitCode.ShouldBe(0, "A path that is blocked is what the sandbox should do.");
        output.ShouldContain("5 probes: 5 good, 0 not good, 0 skipped.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_exposed_path_alone_is_not_good_and_fails_the_run()
    {
        // Arrange
        using var repository = CreateRepository("""{"version":1,"deniedReadPaths":["/etc/shadow"]}""");
        var environment = new FakeSandboxEnvironment();
        environment.Reads["/etc/shadow"] = (ProbeOutcome.Allowed, "content readable");

        // Act
        var (exitCode, output, _) = await Run(repository.Path, environment);

        // Assert
        exitCode.ShouldBe(1);
        output.ShouldContain("5 probes: 4 good, 1 not good, 0 skipped.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_configured_path_with_nothing_to_read_is_skipped_not_called_blocked()
    {
        // Arrange
        using var repository = CreateRepository("""{"version":1,"deniedReadPaths":["~/.ssh"]}""");

        // Act
        var (exitCode, output, _) = await Run(repository.Path, new FakeSandboxEnvironment());

        // Assert
        exitCode.ShouldBe(0, "A path that cannot be told apart from an empty one proves nothing either way.");
        output.ShouldContain("skipped denied-read ~/.ssh:", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_a_configuration_file_the_denied_read_check_is_reported_as_skipped()
    {
        // Arrange
        using var repository = CreateRepository(null);

        // Act
        var (_, output, _) = await Run(repository.Path, new FakeSandboxEnvironment());

        // Assert
        output.ShouldContain("skipped denied reads: .config/sharedkernel-sandbox.json does not exist", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"version":2,"deniedReadPaths":[]}""", "$.version must be 1.")]
    [InlineData("""{"version":1,"deniedReadPaths":["relative/path"]}""", "$.deniedReadPaths[0] must be an absolute path or start with '~/'.")]
    [InlineData("""{"version":1,"deniedReadPaths":[""]}""", "$.deniedReadPaths[0] must be an absolute path or start with '~/'.")]
    [InlineData("""{"version":1}""", "is not valid at")]
    [InlineData("""{"version":1,"deniedReadPaths":[],"extra":1}""", "is not valid at")]
    [InlineData("not json", "is not valid at")]
    public async Task An_invalid_configuration_is_reported_with_its_json_path_and_nothing_is_probed(string configuration, string expected)
    {
        // Arrange
        using var repository = CreateRepository(configuration);
        var environment = new FakeSandboxEnvironment();

        // Act
        var (exitCode, output, error) = await Run(repository.Path, environment);

        // Assert
        exitCode.ShouldBe(2);
        output.ShouldBe(string.Empty);
        error.ShouldContain(expected, StringComparison.Ordinal);
        environment.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_real_environment_runs_every_probe_without_throwing_and_leaves_no_probe_file_behind()
    {
        // Arrange
        using var repository = CreateRepository(null);
        var temp = Path.GetTempPath();
        var before = Directory.GetFiles(temp, ".sharedkernel-sandbox-*").Length;

        // Act
        var (_, output, error) = await Run(repository.Path, new SystemSandboxEnvironment());

        // Assert
        error.ShouldBe(string.Empty);
        output.ShouldContain("unix-socket:", StringComparison.Ordinal);
        output.ShouldContain("named-pipe:", StringComparison.Ordinal);
        output.ShouldContain("temp-write:", StringComparison.Ordinal);
        Directory.GetFiles(temp, ".sharedkernel-sandbox-*").Length.ShouldBe(before, "The probe file is deleted again.");
    }

    [Fact]
    public async Task A_temporary_folder_that_does_not_exist_fails_the_run_because_builds_would_fail()
    {
        // Arrange
        using var repository = CreateRepository(null);
        var environment = new FakeSandboxEnvironment();
        environment.Writes["/tmp/x"] = (ProbeOutcome.Skipped, "The folder does not exist.");

        // Act
        var (exitCode, output, _) = await Run(repository.Path, environment);

        // Assert
        exitCode.ShouldBe(1);
        output.ShouldContain("failed temp-write: The temporary folder does not exist or cannot be seen.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_root_that_does_not_exist_is_an_error_so_a_typo_cannot_skip_the_denied_read_check()
    {
        // Arrange
        var environment = new FakeSandboxEnvironment();
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        // Act
        var (exitCode, output, error) = await Run(missing, environment);

        // Assert
        exitCode.ShouldBe(2);
        output.ShouldBe(string.Empty);
        error.ShouldContain("The root directory does not exist.", StringComparison.Ordinal);
        environment.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_tilde_path_is_skipped_with_a_reason_when_the_home_folder_is_unknown()
    {
        // Arrange
        using var repository = CreateRepository("""{"version":1,"deniedReadPaths":["~/.ssh"]}""");
        var environment = new FakeSandboxEnvironment { HomeDirectory = string.Empty };

        // Act
        var (exitCode, output, _) = await Run(repository.Path, environment);

        // Assert
        exitCode.ShouldBe(0);
        output.ShouldContain("skipped denied-read ~/.ssh: The home folder is unknown", StringComparison.Ordinal);
        environment.Calls.ShouldNotContain(static call => call.StartsWith("read ", StringComparison.Ordinal), "A relative path would have been probed.");
    }

    [Fact]
    public async Task A_configured_path_with_a_direction_override_character_is_printed_escaped()
    {
        // Arrange
        using var repository = CreateRepository("""{"version":1,"deniedReadPaths":["/x\u202E"]}""");

        // Act
        var (_, output, _) = await Run(repository.Path, new FakeSandboxEnvironment());

        // Assert
        output.ShouldNotContain("\u202E", StringComparison.Ordinal);
        output.ShouldContain("denied-read /x", StringComparison.Ordinal);
    }
}
