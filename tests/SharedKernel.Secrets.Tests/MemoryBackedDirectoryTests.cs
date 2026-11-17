namespace PandaBear.Secrets.Tests;

/// <summary>Tests the private directory in which edit lets sops and the editor keep plaintext.</summary>
[Trait("Capability", "Secrets")]
public sealed class MemoryBackedDirectoryTests
{
    [Fact(SkipType = typeof(MemoryFileSystem), SkipUnless = nameof(MemoryFileSystem.SharedMemoryAvailable), Skip = "/dev/shm is not a tmpfs here.")]
    public void A_private_directory_is_created_owner_only_under_a_tmpfs_root_and_removed()
    {
        // Act
        var directory = MemoryBackedDirectory.Create("/dev/shm");
        var mode = OperatingSystem.IsWindows() ? default : File.GetUnixFileMode(directory);
        MemoryBackedDirectory.Remove(directory);

        // Assert
        Path.GetDirectoryName(directory).ShouldBe("/dev/shm");
        mode.ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Directory.Exists(directory).ShouldBeFalse();
    }

    [Fact(SkipType = typeof(MemoryFileSystem), SkipUnless = nameof(MemoryFileSystem.DiskAvailable), Skip = "The test output directory is not on a disk.")]
    public void A_root_on_a_disk_is_refused()
    {
        // Act
        var create = () => MemoryBackedDirectory.Create(MemoryFileSystem.Disk);

        // Assert
        create.ShouldThrow<InvalidOperationException>().Message.ShouldContain("is not on a memory-backed file system", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("relative")]
    [InlineData("/no/such/directory")]
    public void A_root_that_is_relative_or_missing_is_refused(string root)
    {
        // Act
        var create = () => MemoryBackedDirectory.Create(root);

        // Assert
        create.ShouldThrow<InvalidOperationException>();
    }

    [Fact(SkipType = typeof(MemoryFileSystem), SkipUnless = nameof(MemoryFileSystem.SharedMemoryAvailable), Skip = "/dev/shm is not a tmpfs here.")]
    public void A_root_that_is_a_link_is_refused()
    {
        // Arrange
        using var link = new TemporaryLink("/dev/shm", "/dev/shm");

        // Act
        var create = () => MemoryBackedDirectory.Create(link.Path);

        // Assert
        create.ShouldThrow<InvalidOperationException>().Message.ShouldContain("not a link", StringComparison.Ordinal);
    }

    [Fact(SkipType = typeof(MemoryFileSystem), SkipUnless = nameof(MemoryFileSystem.SharedMemoryAvailable), Skip = "/dev/shm is not a tmpfs here.")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void A_read_only_directory_that_an_editor_left_behind_is_still_removed()
    {
        // Arrange
        var directory = MemoryBackedDirectory.Create("/dev/shm");
        var locked = Directory.CreateDirectory(Path.Combine(directory, "backup")).FullName;
        File.WriteAllText(Path.Combine(locked, "plain.txt"), "plaintext");
        File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserExecute);

        // Act
        MemoryBackedDirectory.Remove(directory);

        // Assert
        Directory.Exists(directory).ShouldBeFalse();
    }
}
