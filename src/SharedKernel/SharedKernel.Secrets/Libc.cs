using System.Runtime.InteropServices;

namespace PandaBear.Secrets;

/// <summary>
/// The glibc functions that .NET does not expose: an atomic exchange of two names, and an fsync of a directory, which
/// <see cref="Microsoft.Win32.SafeHandles.SafeFileHandle"/> refuses to open. Names and signatures follow the C headers.
/// The library is looked up only where the system loader looks, never next to the application, so a planted copy is not
/// loaded.
/// </summary>
internal static partial class Libc
{
    /// <summary>The <c>dirfd</c> that makes a relative path resolve from the working directory.</summary>
    internal const int AT_FDCWD = -100;

    /// <summary>The <c>renameat2</c> flag that swaps two existing names atomically.</summary>
    internal const uint RENAME_EXCHANGE = 2;

    /// <summary>The <c>open</c> flag for reading only.</summary>
    internal const int O_RDONLY = 0;

    /// <summary>The error a file system returns for a flag or an operation it does not support.</summary>
    internal const int EINVAL = 22;

    /// <summary>The error of a system call that the kernel does not have.</summary>
    internal const int ENOSYS = 38;

    /// <summary>The error of an operation that the file system does not support.</summary>
    internal const int EOPNOTSUPP = 95;

    private const string Library = "libc.so.6";

    /// <summary><c>int renameat2(int olddirfd, const char *oldpath, int newdirfd, const char *newpath, unsigned int flags)</c>, glibc 2.28 and later.</summary>
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    internal static partial int renameat2(int olddirfd, string oldpath, int newdirfd, string newpath, uint flags);

    /// <summary><c>int open(const char *pathname, int flags)</c>, without the optional mode, which only creating a file needs.</summary>
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    internal static partial FileDescriptorHandle open(string pathname, int flags);

    /// <summary><c>int fsync(int fd)</c>.</summary>
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport(Library, SetLastError = true)]
    internal static partial int fsync(FileDescriptorHandle fd);

    /// <summary><c>int close(int fd)</c>.</summary>
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport(Library, SetLastError = true)]
    internal static partial int close(nint fd);
}
