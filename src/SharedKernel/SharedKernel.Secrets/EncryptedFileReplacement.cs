using System.Security.Cryptography;
using System.Text;

namespace PandaBear.Secrets;

/// <summary>
/// Replaces an encrypted file with a new version through a temporary file in the same directory and a rename, and only
/// when the file still holds the bytes the operation started from. A failure never leaves a partial version, and a change
/// made before the final comparison is refused; one made between that comparison and the rename is lost. The new
/// version's data is flushed to disk before the rename, but the directory is not, so a crash right after the rename can
/// leave the previous version in place.
/// </summary>
internal static class EncryptedFileReplacement
{
    /// <summary>Writes <paramref name="document"/> in place of <paramref name="destination"/> when it still holds <paramref name="expected"/>.</summary>
    /// <exception cref="InvalidOperationException">The file changed since it was read.</exception>
    public static Task Replace(string destination, byte[] expected, string document, TimeSpan readTimeout, CancellationToken cancellationToken) =>
        Replace(destination, expected, document, readTimeout, beforeSwap: null, cancellationToken);

    /// <summary>Replaces the file like the other overload, and calls <paramref name="beforeSwap"/> just before the new version takes its place.</summary>
    /// <exception cref="InvalidOperationException">The file changed since it was read.</exception>
    internal static async Task Replace(string destination, byte[] expected, string document, TimeSpan readTimeout, Action? beforeSwap, CancellationToken cancellationToken)
    {
        var (current, problem) = await GuardedFileReader.Read(destination, SopsFileLimits.MaxBytes, readTimeout, cancellationToken).ConfigureAwait(false);
        if (problem is not null || !current.AsSpan().SequenceEqual(expected))
        {
            throw new InvalidOperationException("The file changed while the operation ran; nothing was written. Run it again.");
        }

        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".{RandomNumberGenerator.GetHexString(32)}.tmp");
        FileStreamOptions options = new()
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
        };
        var mode = OperatingSystem.IsWindows() ? default : File.GetUnixFileMode(destination);
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        try
        {
            var stream = new FileStream(temporary, options);
            await using (stream.ConfigureAwait(false))
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes(document), cancellationToken).ConfigureAwait(false);
                await Task.Run(() => stream.Flush(flushToDisk: true), cancellationToken).ConfigureAwait(false);
            }

            // The creation mode passes through the umask, so the mode of the file being replaced is set explicitly.
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(temporary, mode);
            }

            beforeSwap?.Invoke();
            File.Move(temporary, destination, overwrite: true);
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }
    }
}
