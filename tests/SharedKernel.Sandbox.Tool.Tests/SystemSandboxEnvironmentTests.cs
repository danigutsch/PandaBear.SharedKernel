namespace PandaBear.Sandbox.Tool.Tests;

/// <summary>Tests the real environment's probes against temporary folders whose permissions force each outcome.</summary>
[Trait("Capability", "Sandbox")]
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
        using var variable = new TemporaryEnvironmentVariable("NUGET_PACKAGES", absolute);
        var environment = new SystemSandboxEnvironment();

        // Act
        var configured = environment.NuGetPackagesDirectory;
        variable.Set("relative/folder");
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

    [Fact]
    public async Task A_temporary_folder_path_too_long_for_a_socket_path_is_a_failure_not_a_crash_and_the_path_is_not_printed()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var longFolder = Path.Combine(directory.Path, new string('a', 60), new string('b', 60));
        Directory.CreateDirectory(longFolder);

        // Act
        var (_, output, error) = await DoctorProcess.Run(longFolder, TestContext.Current.CancellationToken);

        // Assert: a sandbox that refuses sockets answers before the path is checked, so denied is also correct there.
        error.ShouldNotContain("Unhandled exception", StringComparison.Ordinal);
        output.ShouldNotContain(directory.Path, StringComparison.Ordinal);
        var lines = output.Split('\n').Where(static l => l.Contains("unix-socket:", StringComparison.Ordinal) || l.Contains("named-pipe:", StringComparison.Ordinal)).ToArray();
        lines.Length.ShouldBe(2, "Both socket probes print a result instead of crashing.");
        var allAnswered = lines.All(static l => l.StartsWith("failed ", StringComparison.Ordinal) || l.StartsWith("denied ", StringComparison.Ordinal));
        allAnswered.ShouldBeTrue("Each probe says failed or denied for a path that is too long or a sandbox that refuses sockets.");
    }

    [Fact(SkipType = typeof(SocketsProbe), SkipUnless = nameof(SocketsProbe.IsAvailable), Skip = "This environment does not allow Unix sockets.")]
    public async Task Where_sockets_are_allowed_the_probes_bind_connect_and_exchange_a_byte_and_leave_nothing_behind()
    {
        // Arrange
        using var folder = new ShortTemporaryDirectory();

        // Act
        var (_, output, _) = await DoctorProcess.Run(folder.Path, TestContext.Current.CancellationToken);

        // Assert
        output.ShouldContain("allowed unix-socket: A Unix domain socket can be created, bound, connected, and used.", StringComparison.Ordinal);
        output.ShouldContain("allowed named-pipe: A named pipe can be created, connected, and used.", StringComparison.Ordinal);
        Directory.GetFileSystemEntries(folder.Path).ShouldBeEmpty("The socket files are removed again.");
    }

    [Fact(SkipType = typeof(PermissionsProbe), SkipUnless = nameof(PermissionsProbe.CanRestrict), Skip = "File permissions are not enforced for this process.")]
    public async Task A_temporary_folder_that_cannot_be_written_is_denied_for_sockets_and_pipes_and_names_the_write_probe_too()
    {
        // Arrange
        using var folder = new ShortTemporaryDirectory();
        using var restricted = new RestrictedPermissions(folder.Path, UnixFileMode.UserRead | UnixFileMode.UserExecute, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        // Act
        var (_, output, _) = await DoctorProcess.Run(folder.Path, TestContext.Current.CancellationToken);

        // Assert: a sandbox that refuses sockets altogether answers at the first stage, a permissive one when binding.
        output.ShouldContain("denied unix-socket:", StringComparison.Ordinal);
        output.ShouldContain("denied named-pipe:", StringComparison.Ordinal);
        output.ShouldContain("denied temp-write:", StringComparison.Ordinal);
        output.ShouldNotContain(folder.Path, StringComparison.Ordinal);
    }
}
