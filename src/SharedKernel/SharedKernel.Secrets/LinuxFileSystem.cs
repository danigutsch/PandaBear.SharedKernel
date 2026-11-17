using System.Runtime.InteropServices;

namespace PandaBear.Secrets;

/// <summary>
/// File operations that only Linux offers through glibc: an atomic exchange of two names and a durable directory. Where
/// the library, the call, or the file system does not support one, the caller is told so and takes its portable path.
/// </summary>
internal static class LinuxFileSystem
{
    /// <summary>
    /// Swaps the files at <paramref name="first"/> and <paramref name="second"/> in one step, so that no other writer can
    /// change either name in between.
    /// </summary>
    /// <returns><see langword="false"/> when the platform or the file system cannot exchange names.</returns>
    /// <exception cref="IOException">The exchange failed for another reason, such as a missing file.</exception>
    public static bool TryExchange(string first, string second)
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        int result;
        int error;
        try
        {
            result = Libc.renameat2(Libc.AT_FDCWD, first, Libc.AT_FDCWD, second, Libc.RENAME_EXCHANGE);
            error = Marshal.GetLastPInvokeError();
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }

        return result == 0 || (error is Libc.EINVAL or Libc.ENOSYS or Libc.EOPNOTSUPP
            ? false
            : throw new IOException($"The new version could not take the file's place: {Marshal.GetPInvokeErrorMessage(error)}"));
    }

    /// <summary>
    /// Flushes <paramref name="directory"/> to disk, so that a rename in it survives a crash. A file system that cannot
    /// flush a directory is skipped, as is a platform without glibc.
    /// </summary>
    /// <exception cref="IOException">The directory could not be opened or flushed.</exception>
    public static void FlushDirectory(string directory)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        try
        {
            Flush(directory);
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            // Without glibc there is no way to flush a directory; the rename stands as the file system keeps it.
        }
    }

    private static void Flush(string directory)
    {
        using var descriptor = Libc.open(directory, Libc.O_RDONLY);
        var error = Marshal.GetLastPInvokeError();
        if (descriptor.IsInvalid)
        {
            throw new IOException($"The directory could not be opened to flush it to disk: {Marshal.GetPInvokeErrorMessage(error)}");
        }

        if (Libc.fsync(descriptor) != 0)
        {
            error = Marshal.GetLastPInvokeError();
            if (error != Libc.EINVAL)
            {
                throw new IOException($"The directory could not be flushed to disk: {Marshal.GetPInvokeErrorMessage(error)}");
            }
        }
    }
}
