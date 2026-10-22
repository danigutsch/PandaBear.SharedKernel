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
}
