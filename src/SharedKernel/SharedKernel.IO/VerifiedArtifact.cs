// Adapted from ViajantesTurismo under its MIT license. See LICENSE.ViajantesTurismo.txt.

using System.Security.Cryptography;

namespace PandaBear.IO;

/// <summary>Verifies manifest-relative artifacts against their reviewed SHA-256 digests.</summary>
/// <remarks>
/// Keep the root and its contents in a directory controlled by the caller. Path checks reject existing reparse points
/// and cannot prevent concurrent replacement of path segments by another writer.
/// </remarks>
public static class VerifiedArtifact
{
    /// <summary>Streams a file into SHA-256 and checks its digest.</summary>
    /// <param name="directory">The trusted directory the path is relative to.</param>
    /// <param name="path">A forward-slash-separated relative file path.</param>
    /// <param name="sha256">The expected digest as 64 hexadecimal digits.</param>
    /// <param name="ct">Cancels verification.</param>
    /// <exception cref="ArgumentException">The directory is null or empty.</exception>
    /// <exception cref="InvalidDataException">The path or digest is invalid, or the digest does not match.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">Access is denied.</exception>
    /// <exception cref="OperationCanceledException">Verification was canceled.</exception>
    public static async Task Verify(string directory, string path, string sha256, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var resolved = ResolvePath(directory, path, sha256);
        var stream = File.OpenRead(resolved);
        await using (stream.ConfigureAwait(false))
        {
            VerifyDigest(await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false), sha256);
        }
    }

    /// <summary>Reads a bounded file and verifies the same bytes returned to the caller.</summary>
    /// <param name="directory">The trusted directory the path is relative to.</param>
    /// <param name="path">A forward-slash-separated relative file path.</param>
    /// <param name="sha256">The expected digest as 64 hexadecimal digits.</param>
    /// <param name="maxBytes">The largest accepted file size.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>The verified file bytes.</returns>
    /// <exception cref="ArgumentException">The directory is null or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The size limit is negative.</exception>
    /// <exception cref="InvalidDataException">The path or digest is invalid, the size limit is exceeded, or the digest does not match.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">Access is denied.</exception>
    /// <exception cref="OperationCanceledException">The read was canceled.</exception>
    public static async Task<ReadOnlyMemory<byte>> Read(string directory, string path, string sha256, int maxBytes, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);
        ct.ThrowIfCancellationRequested();
        var resolved = ResolvePath(directory, path, sha256);
        var content = await BoundedReader.ReadFile(resolved, maxBytes, ct).ConfigureAwait(false)
            ?? throw new InvalidDataException("Migration artifact exceeds the size limit.");
        VerifyDigest(SHA256.HashData(content), sha256);
        return content;
    }

    private static string ResolvePath(string directory, string path, string sha256)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        if (sha256 is not { Length: 64 } || !sha256.All(char.IsAsciiHexDigit))
        {
            throw new InvalidDataException("Artifact digest must be a SHA-256 value.");
        }

        var segments = path?.Split('/');
        if (segments is null || segments.Any(static segment => segment is "" or "." or ".."
                || segment.Any(static character => character is '\\' or ':' || char.IsControl(character))))
        {
            throw new InvalidDataException("Artifact path must stay inside the manifest directory.");
        }

        var resolved = directory;
        var attributes = default(FileAttributes);
        foreach (var segment in segments)
        {
            resolved = Path.Combine(resolved, segment);
            attributes = File.GetAttributes(resolved);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Artifact paths cannot contain symbolic links.");
            }
        }

        if ((attributes & FileAttributes.Directory) != 0)
        {
            throw new InvalidDataException("An artifact must be a file.");
        }

        return resolved;
    }

    private static void VerifyDigest(byte[] digest, string expected)
    {
        if (!string.Equals(Convert.ToHexString(digest), expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Artifact digest mismatch.");
        }
    }
}
