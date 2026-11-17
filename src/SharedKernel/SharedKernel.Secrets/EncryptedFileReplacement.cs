using System.Security.Cryptography;
using System.Text;
using PandaBear.IO;

namespace PandaBear.Secrets;

/// <summary>
/// Replaces an encrypted file with a new version through a temporary file in the same directory and a rename, and only
/// when the file still holds the bytes the operation started from, so neither a failure nor a concurrent change leaves a
/// partial or lost version.
/// </summary>
internal static class EncryptedFileReplacement
{
    private const int MaxBytes = 16 * 1024 * 1024;

    /// <summary>Writes <paramref name="document"/> in place of <paramref name="destination"/> when it still holds <paramref name="expected"/>.</summary>
    /// <exception cref="InvalidOperationException">The file changed since it was read.</exception>
    public static async Task Replace(string destination, byte[] expected, string document, CancellationToken cancellationToken)
    {
        var current = await BoundedReader.ReadFile(destination, MaxBytes, cancellationToken).ConfigureAwait(false);
        if (current is null || !current.AsSpan().SequenceEqual(expected))
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
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = File.GetUnixFileMode(destination);
        }

        try
        {
            var stream = new FileStream(temporary, options);
            await using (stream.ConfigureAwait(false))
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes(document), cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporary, destination, overwrite: true);
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }
    }
}
