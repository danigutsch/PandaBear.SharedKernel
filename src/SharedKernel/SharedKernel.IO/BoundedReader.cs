namespace PandaBear.IO;

/// <summary>
/// Reads a whole file or stream into memory only when it holds no more than a limit, for small inputs such as
/// configuration files, reports, and API responses.
/// </summary>
public static class BoundedReader
{
    private const int BufferSize = 81_920;

    /// <summary>
    /// Reads the file at <paramref name="path"/>, or returns <see langword="null"/> when it has more than
    /// <paramref name="maxBytes"/> bytes, also when it grows past the limit while it is read. Other processes may write,
    /// rename, or delete the file during the read.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <param name="maxBytes">The largest number of bytes accepted.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The content, or <see langword="null"/> when the file holds more than <paramref name="maxBytes"/> bytes.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxBytes"/> is negative.</exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="DirectoryNotFoundException">A directory on the path does not exist.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The path names a directory, or access is denied.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static async Task<byte[]?> ReadFile(string path, int maxBytes, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);
        var stream = new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.ReadWrite | FileShare.Delete,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
        });
        await using (stream.ConfigureAwait(false))
        {
            return await ReadToEnd(stream, maxBytes, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads <paramref name="source"/> from its current position to its end, or returns <see langword="null"/> as soon
    /// as it has more than <paramref name="maxBytes"/> bytes. A seekable stream whose remaining length is already over the
    /// limit is refused without reading; any stream that grows while it is read is refused when the limit is passed.
    /// </summary>
    /// <param name="source">The stream to read. It stays open and is owned by the caller.</param>
    /// <param name="maxBytes">The largest number of bytes accepted.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bytes read, or <see langword="null"/> when the stream holds more than <paramref name="maxBytes"/> bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxBytes"/> is negative.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static async Task<byte[]?> ReadToEnd(Stream source, int maxBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);
        if (source.CanSeek && source.Length - source.Position > maxBytes)
        {
            return null;
        }

        var content = new MemoryStream();
        await using (content.ConfigureAwait(false))
        {
            var buffer = new byte[BufferSize];
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (content.Length + read > maxBytes)
                {
                    return null;
                }

                await content.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            return content.ToArray();
        }
    }
}
