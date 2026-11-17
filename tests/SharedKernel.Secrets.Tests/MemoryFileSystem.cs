namespace PandaBear.Secrets.Tests;

/// <summary>Tells tests where a memory-backed file system and a disk are.</summary>
internal static class MemoryFileSystem
{
    /// <summary>Gets a value indicating whether <c>/dev/shm</c> is a tmpfs.</summary>
    public static bool SharedMemoryAvailable => !OperatingSystem.IsWindows() && Directory.Exists("/dev/shm") && new DriveInfo("/dev/shm").DriveFormat == "tmpfs";

    /// <summary>Gets a value indicating whether the test output directory is on a disk.</summary>
    public static bool DiskAvailable => !OperatingSystem.IsWindows() && new DriveInfo(AppContext.BaseDirectory).DriveFormat is not ("tmpfs" or "ramfs");

    /// <summary>Gets a value indicating whether files have Unix modes.</summary>
    public static bool UnixFileModes => !OperatingSystem.IsWindows();

    /// <summary>Gets a value indicating whether the platform is Linux.</summary>
    public static bool Linux => OperatingSystem.IsLinux();

    /// <summary>Gets the test output directory.</summary>
    public static string Disk => AppContext.BaseDirectory.TrimEnd('/');
}
