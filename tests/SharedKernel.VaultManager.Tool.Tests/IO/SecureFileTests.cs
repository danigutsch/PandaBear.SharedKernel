using PandaBear.VaultManager.Tool.IO;

namespace PandaBear.VaultManager.Tool.Tests.IO;

public sealed class SecureFileTests : IDisposable
{
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private readonly string directory = Directory.CreateTempSubdirectory("vault-manager-tests-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public void WriteAllText_creates_file_readable_only_by_owner()
    {
        var path = Path.Combine(directory, "out.json");

        SecureFile.WriteAllText(path, "content", overwrite: false);

        File.ReadAllText(path).ShouldBe("content");
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(path).ShouldBe(OwnerOnly);
        }
    }

    [Fact]
    public void WriteAllText_refuses_to_replace_existing_file_without_overwrite()
    {
        var path = Path.Combine(directory, "out.json");
        File.WriteAllText(path, "existing");

        var action = () => SecureFile.WriteAllText(path, "new", overwrite: false);

        action.ShouldThrow<IOException>();
        File.ReadAllText(path).ShouldBe("existing");
    }

    [Fact]
    public void WriteAllText_overwrite_replaces_content_and_narrows_permissions()
    {
        var path = Path.Combine(directory, "out.json");
        File.WriteAllText(path, "existing");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, OwnerOnly | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        }

        SecureFile.WriteAllText(path, "new", overwrite: true);

        File.ReadAllText(path).ShouldBe("new");
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(path).ShouldBe(OwnerOnly);
        }
    }

    [Fact]
    public void WriteAllText_leaves_no_temporary_files()
    {
        var path = Path.Combine(directory, "out.json");

        SecureFile.WriteAllText(path, "content", overwrite: false);

        Directory.GetFiles(directory).ShouldBe([path]);
    }

    [Fact]
    public void WriteAllText_with_a_token_canceled_before_the_move_leaves_neither_a_temporary_file_nor_the_file()
    {
        var path = Path.Combine(directory, "out.json");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = () => SecureFile.WriteAllText(path, "content".AsSpan(), overwrite: false, cancellation.Token);

        action.ShouldThrow<OperationCanceledException>();
        Directory.GetFileSystemEntries(directory).ShouldBeEmpty();
    }

    [Fact]
    public void WriteAllText_with_a_canceled_token_stops_before_it_creates_the_temporary_file()
    {
        // Creating the temporary file in a missing directory would fail, so only a write that stops first reports the
        // cancellation.
        var path = Path.Combine(directory, "missing", "out.json");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = () => SecureFile.WriteAllText(path, "content".AsSpan(), overwrite: false, cancellation.Token);

        action.ShouldThrow<OperationCanceledException>();
        Directory.GetFileSystemEntries(directory).ShouldBeEmpty();
    }

    [Fact]
    public void WriteAllText_with_a_token_canceled_before_the_move_keeps_the_file_it_would_replace()
    {
        var path = Path.Combine(directory, "out.json");
        File.WriteAllText(path, "existing");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = () => SecureFile.WriteAllText(path, "new".AsSpan(), overwrite: true, cancellation.Token);

        action.ShouldThrow<OperationCanceledException>();
        File.ReadAllText(path).ShouldBe("existing");
        Directory.GetFileSystemEntries(directory).ShouldBe([path]);
    }

    [Fact]
    public void WriteAllText_deletes_its_temporary_file_when_the_file_cannot_be_moved_into_place()
    {
        var path = Directory.CreateDirectory(Path.Combine(directory, "out.json")).FullName;

        var action = () => SecureFile.WriteAllText(path, "content", overwrite: true);

        action.ShouldThrow<IOException>();
        Directory.GetFileSystemEntries(directory).ShouldBe([path]);
        Directory.GetFileSystemEntries(path).ShouldBeEmpty();
    }
}
