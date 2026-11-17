namespace PandaBear.Secrets.Tool.Tests;

/// <summary>A new directory under <c>/dev/shm</c>, a memory-backed file system, for the temporary root of <c>edit</c>.</summary>
internal sealed class MemoryRoot : IDisposable
{
    private MemoryRoot(string path)
    {
        Path = path;
        Directory.CreateDirectory(path);
    }

    /// <summary>Gets a value indicating whether <c>/dev/shm</c> is a tmpfs.</summary>
    public static bool Available => !OperatingSystem.IsWindows() && Directory.Exists("/dev/shm") && new DriveInfo("/dev/shm").DriveFormat == "tmpfs";

    /// <summary>Gets a value indicating whether the test output directory is on a disk rather than in memory.</summary>
    public static bool DiskAvailable => !OperatingSystem.IsWindows() && new DriveInfo(AppContext.BaseDirectory).DriveFormat is not ("tmpfs" or "ramfs");

    /// <summary>Gets a directory on a disk: the test output directory.</summary>
    public static string Disk => AppContext.BaseDirectory.TrimEnd('/');

    /// <summary>Gets the absolute path.</summary>
    public string Path
    {
        get;
    }

    /// <summary>Creates the directory under <c>/dev/shm</c>.</summary>
    public static MemoryRoot Create() => new(System.IO.Path.Combine("/dev/shm", "sharedkernel-secrets-tests-" + Guid.NewGuid().ToString("N")));

    /// <summary>The variables of a terminal session whose temporary root is <paramref name="temporaryRoot"/>, with an editor, an identity, and a forge token that must not reach the editor.</summary>
    public static Dictionary<string, string> Variables(string temporaryRoot) => new()
    {
        ["SOPS_AGE_KEY_FILE"] = "/keys/id.txt",
        ["EDITOR"] = "vim -n",
        ["XDG_RUNTIME_DIR"] = temporaryRoot,
        ["TERM"] = "xterm-256color",
        ["GH_TOKEN"] = "token",
    };

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(Path, recursive: true);
}
