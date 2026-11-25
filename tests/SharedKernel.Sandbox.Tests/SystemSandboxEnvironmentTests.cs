namespace PandaBear.Sandbox.Tests;

/// <summary>Tests the real environment's probes against temporary folders whose permissions force each outcome.</summary>
[Trait(TestTraitNames.CapabilityName, "Sandbox")]
[Collection(EnvironmentCollection.Name)]
public sealed class SystemSandboxEnvironmentTests
{
    [Fact]
    public void A_writable_folder_is_allowed_and_no_probe_file_is_left_behind()
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        // Act
        var (outcome, detail) = new SystemSandboxEnvironment().WriteFile(directory.Path);

        // Assert
        outcome.ShouldBe(ProbeOutcome.Allowed);
        detail.ShouldNotBeEmpty();
        Directory.GetFileSystemEntries(directory.Path).ShouldBeEmpty();
    }

    [Fact]
    public void A_missing_folder_is_skipped_when_writing()
    {
        // Act
        var (outcome, _) = new SystemSandboxEnvironment().WriteFile(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        // Assert
        outcome.ShouldBe(ProbeOutcome.Skipped);
    }

    [Fact(SkipType = typeof(PermissionsProbe), SkipUnless = nameof(PermissionsProbe.CanRestrict), Skip = "File permissions are not enforced for this process.")]
    public void A_folder_that_cannot_be_written_is_denied()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        using var restricted = new RestrictedPermissions(directory.Path, UnixFileMode.UserRead | UnixFileMode.UserExecute, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        // Act
        var (outcome, _) = new SystemSandboxEnvironment().WriteFile(directory.Path);

        // Assert
        outcome.ShouldBe(ProbeOutcome.Denied);
    }

    [Fact]
    public void A_missing_path_and_an_empty_folder_and_an_empty_file_cannot_be_told_from_hidden_ones_and_are_skipped()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var emptyFile = Path.Combine(directory.Path, "empty");
        File.WriteAllBytes(emptyFile, []);
        var emptyFolder = Path.Combine(directory.Path, "folder");
        Directory.CreateDirectory(emptyFolder);
        var environment = new SystemSandboxEnvironment();

        // Act
        var missing = environment.ReadPath(Path.Combine(directory.Path, "missing"));
        var file = environment.ReadPath(emptyFile);
        var folder = environment.ReadPath(emptyFolder);

        // Assert
        missing.Outcome.ShouldBe(ProbeOutcome.Skipped);
        file.Outcome.ShouldBe(ProbeOutcome.Skipped);
        folder.Outcome.ShouldBe(ProbeOutcome.Skipped);
    }

    [Fact]
    public void A_file_with_content_and_a_folder_with_entries_are_readable()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "data");
        File.WriteAllText(file, "content");
        var environment = new SystemSandboxEnvironment();

        // Act
        var readFile = environment.ReadPath(file);
        var readFolder = environment.ReadPath(directory.Path);

        // Assert
        readFile.Outcome.ShouldBe(ProbeOutcome.Allowed);
        readFolder.Outcome.ShouldBe(ProbeOutcome.Allowed);
    }

    [Fact(SkipType = typeof(PermissionsProbe), SkipUnless = nameof(PermissionsProbe.CanRestrict), Skip = "File permissions are not enforced for this process.")]
    public void A_file_and_a_folder_that_the_process_may_not_read_are_denied()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "secret");
        File.WriteAllText(file, "content");
        var folder = Path.Combine(directory.Path, "closed");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "inner"), "content");
        using var closedFile = new RestrictedPermissions(file, UnixFileMode.None, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        using var closedFolder = new RestrictedPermissions(folder, UnixFileMode.None, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var environment = new SystemSandboxEnvironment();

        // Act
        var readFile = environment.ReadPath(file);
        var readFolder = environment.ReadPath(folder);

        // Assert
        readFile.Outcome.ShouldBe(ProbeOutcome.Denied);
        readFolder.Outcome.ShouldBe(ProbeOutcome.Denied);
    }

    [Fact]
    public void Creating_a_socket_and_a_pipe_gives_a_clear_answer_in_every_environment()
    {
        // Arrange
        var environment = new SystemSandboxEnvironment();

        // Act
        var socket = environment.CreateUnixSocket();
        var pipe = environment.CreateNamedPipe();

        // Assert
        socket.Outcome.ShouldBeOneOf(ProbeOutcome.Allowed, ProbeOutcome.Denied, ProbeOutcome.Skipped);
        pipe.Outcome.ShouldBeOneOf(ProbeOutcome.Allowed, ProbeOutcome.Denied);
        socket.Detail.ShouldNotBeEmpty();
        pipe.Detail.ShouldNotBeEmpty();
    }

    [Fact]
    public void The_nuget_folder_is_the_configured_absolute_one_and_otherwise_the_default_under_home()
    {
        // Arrange
        var absolute = Path.Combine(Path.GetTempPath(), "nuget-packages-for-test");
        using var configuredVariable = new EnvironmentVariableScope("NUGET_PACKAGES", absolute);
        var environment = new SystemSandboxEnvironment();

        // Act
        var configured = environment.NuGetPackagesDirectory;
        using var relativeVariable = new EnvironmentVariableScope("NUGET_PACKAGES", "relative/folder");
        var relative = environment.NuGetPackagesDirectory;

        // Assert
        configured.ShouldBe(absolute);
        relative.ShouldBe(Path.Combine(environment.HomeDirectory, ".nuget", "packages"), "A relative value is ignored.");
        environment.TempDirectory.ShouldBe(Path.GetTempPath());
    }

    [Fact(SkipType = typeof(SpecialFilesProbe), SkipUnless = nameof(SpecialFilesProbe.CanCreateFifo), Skip = "A FIFO cannot be created here.")]
    public async Task A_fifo_is_skipped_within_the_time_limit_instead_of_blocking_the_probe()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var fifo = Path.Combine(directory.Path, "pipe");
        await SpecialFilesProbe.CreateFifo(fifo, TestContext.Current.CancellationToken);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var (outcome, detail) = new SystemSandboxEnvironment().ReadPath(fifo);

        // Assert
        outcome.ShouldBe(ProbeOutcome.Skipped);
        detail.ShouldContain("cannot be probed", StringComparison.Ordinal);
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(10));
    }

    [Fact(SkipType = typeof(PermissionsProbe), SkipUnless = nameof(PermissionsProbe.CanRestrict), Skip = "File permissions are not enforced for this process.")]
    public void A_folder_that_can_be_listed_but_whose_files_cannot_be_read_is_denied_not_exposed()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "inner");
        File.WriteAllText(file, "content");
        using var closed = new RestrictedPermissions(file, UnixFileMode.None, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        // Act
        var (outcome, detail) = new SystemSandboxEnvironment().ReadPath(directory.Path);

        // Assert
        outcome.ShouldBe(ProbeOutcome.Denied);
        detail.ShouldContain("cannot be read", StringComparison.Ordinal);
    }

    [Fact]
    public void A_folder_with_only_a_subfolder_is_reported_as_listable()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory.Path, "sub"));

        // Act
        var (outcome, _) = new SystemSandboxEnvironment().ReadPath(directory.Path);

        // Assert
        outcome.ShouldBe(ProbeOutcome.Allowed);
    }
}
