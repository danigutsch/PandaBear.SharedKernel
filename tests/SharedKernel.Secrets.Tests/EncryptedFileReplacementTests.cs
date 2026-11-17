using System.Runtime.Versioning;
using System.Text;

namespace PandaBear.Secrets.Tests;

/// <summary>Tests that a new version replaces an encrypted file atomically and only when the file is unchanged.</summary>
[Trait("Capability", "Secrets")]
public sealed class EncryptedFileReplacementTests
{
    [Fact]
    public async Task The_new_version_replaces_the_file_and_leaves_no_temporary_file()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "app.sops.yaml");
        await File.WriteAllTextAsync(file, "old", TestContext.Current.CancellationToken);

        // Act
        await EncryptedFileReplacement.Replace(file, Encoding.UTF8.GetBytes("old"), "new", TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Assert
        var content = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
        content.ShouldBe("new");
        Directory.EnumerateFiles(directory.Path).ShouldHaveSingleItem().ShouldBe(file);
    }

    [Fact]
    public async Task A_file_that_changed_since_it_was_read_is_not_replaced()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "app.sops.yaml");
        await File.WriteAllTextAsync(file, "changed by someone else", TestContext.Current.CancellationToken);

        // Act
        var replace = () => EncryptedFileReplacement.Replace(file, Encoding.UTF8.GetBytes("old"), "new", TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Assert
        var exception = await replace.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldBe("The file changed while the operation ran; nothing was written. Run it again.");
        var content = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
        content.ShouldBe("changed by someone else");
        Directory.EnumerateFiles(directory.Path).ShouldHaveSingleItem();
    }

    [Fact(SkipType = typeof(MemoryFileSystem), SkipUnless = nameof(MemoryFileSystem.UnixFileModes), Skip = "Unix file modes only.")]
    [UnsupportedOSPlatform("windows")]
    public async Task The_new_version_keeps_the_mode_of_the_file_it_replaces()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "app.sops.yaml");
        await File.WriteAllTextAsync(file, "old", TestContext.Current.CancellationToken);
        File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        // Act
        await EncryptedFileReplacement.Replace(file, Encoding.UTF8.GetBytes("old"), "new", TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Assert
        File.GetUnixFileMode(file).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [Fact(SkipType = typeof(MemoryFileSystem), SkipUnless = nameof(MemoryFileSystem.UnixFileModes), Skip = "Unix file modes only.")]
    [UnsupportedOSPlatform("windows")]
    public async Task A_mode_that_the_umask_would_narrow_survives_the_replacement()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "app.sops.yaml");
        await File.WriteAllTextAsync(file, "old", TestContext.Current.CancellationToken);
        const UnixFileMode mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite;
        File.SetUnixFileMode(file, mode);

        // Act
        await EncryptedFileReplacement.Replace(file, Encoding.UTF8.GetBytes("old"), "new", TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Assert
        File.GetUnixFileMode(file).ShouldBe(mode);
    }
}
