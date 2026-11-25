namespace PandaBear.Sandbox.Tool.Tests;

/// <summary>Tests the real probes in a separate tool process with a chosen temporary folder.</summary>
[Trait(TestTraitNames.CapabilityName, "Sandbox")]
[Collection(EnvironmentCollection.Name)]
public sealed class DoctorProcessTests
{
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
