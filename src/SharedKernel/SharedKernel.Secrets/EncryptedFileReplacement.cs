using System.Security.Cryptography;
using System.Text;

namespace PandaBear.Secrets;

/// <summary>
/// Replaces an encrypted file with a new version only when the file still holds the bytes the operation started from. The
/// new version is written to a temporary file in the same directory and flushed to disk. On Linux it then swaps names with
/// the file in one step, and the version it displaced is compared with those bytes: if another writer changed the file
/// before the swap, by a rename or in place, the swap is undone and the operation refused, so that writer's version is
/// kept. Elsewhere, or on a file system that cannot swap names, the file is compared just before a rename instead. The
/// directory is flushed to disk afterwards, so the new name survives a crash.
/// </summary>
internal static class EncryptedFileReplacement
{
    private const string Changed = "The file changed while the operation ran; nothing was written. Run it again.";

    /// <summary>Writes <paramref name="document"/> in place of <paramref name="destination"/> when it still holds <paramref name="expected"/>.</summary>
    /// <exception cref="InvalidOperationException">The file changed since it was read.</exception>
    /// <exception cref="IOException">The new version could not be written, swapped in, or flushed to disk.</exception>
    public static Task Replace(string destination, byte[] expected, string document, TimeSpan readTimeout, CancellationToken cancellationToken) =>
        Replace(destination, expected, document, readTimeout, beforeSwap: null, cancellationToken);

    /// <summary>Replaces the file like the other overload, and calls <paramref name="beforeSwap"/> just before the new version takes its place.</summary>
    /// <exception cref="InvalidOperationException">The file changed since it was read.</exception>
    /// <exception cref="IOException">The new version could not be written, swapped in, or flushed to disk.</exception>
    internal static async Task Replace(string destination, byte[] expected, string document, TimeSpan readTimeout, Action? beforeSwap, CancellationToken cancellationToken)
    {
        if (!await Holds(destination, expected, readTimeout, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException(Changed);
        }

        var directory = Path.GetDirectoryName(destination)!;
        var temporary = Path.Combine(directory, $".{RandomNumberGenerator.GetHexString(32)}.tmp");
        var keepTemporary = false;
        try
        {
            await WriteDurably(temporary, document, OperatingSystem.IsWindows() ? default : File.GetUnixFileMode(destination), cancellationToken).ConfigureAwait(false);
            beforeSwap?.Invoke();
            if (LinuxFileSystem.TryExchange(temporary, destination))
            {
                // The temporary name now holds what was in place; it must be the version the operation started from.
                if (!await Holds(temporary, expected, readTimeout, cancellationToken).ConfigureAwait(false))
                {
                    keepTemporary = !LinuxFileSystem.TryExchange(temporary, destination);
                    throw new InvalidOperationException(keepTemporary
                        ? $"The file changed while the operation ran, and the other version could not be put back; it is kept at {temporary}."
                        : Changed);
                }
            }
            else
            {
                if (!await Holds(destination, expected, readTimeout, cancellationToken).ConfigureAwait(false))
                {
                    throw new InvalidOperationException(Changed);
                }

                File.Move(temporary, destination, overwrite: true);
            }

            LinuxFileSystem.FlushDirectory(directory);
        }
        finally
        {
            if (!keepTemporary)
            {
                File.Delete(temporary);
            }
        }
    }

    private static async Task<bool> Holds(string path, byte[] expected, TimeSpan readTimeout, CancellationToken cancellationToken)
    {
        var (current, problem) = await GuardedFileReader.Read(path, SopsFileLimits.MaxBytes, readTimeout, cancellationToken).ConfigureAwait(false);
        return problem is null && current.AsSpan().SequenceEqual(expected);
    }

    private static async Task WriteDurably(string path, string document, UnixFileMode mode, CancellationToken cancellationToken)
    {
        FileStreamOptions options = new()
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        var stream = new FileStream(path, options);
        await using (stream.ConfigureAwait(false))
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes(document), cancellationToken).ConfigureAwait(false);
            await Task.Run(() => stream.Flush(flushToDisk: true), cancellationToken).ConfigureAwait(false);
        }

        // The creation mode passes through the umask, so the mode of the file being replaced is set explicitly.
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, mode);
        }
    }
}
