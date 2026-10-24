namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Reads repository files up to a size limit.
/// </summary>
internal static class RepositoryFile
{
    private const int BufferSize = 81_920;

    private static ReadOnlySpan<byte> Utf8ByteOrderMark => [0xEF, 0xBB, 0xBF];

    /// <summary>
    /// Reads a file, or returns <see langword="null"/> when it holds more than <paramref name="maxBytes"/> bytes.
    /// </summary>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="DirectoryNotFoundException">A directory on the path does not exist.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The path names a directory, or access is denied.</exception>
    public static async Task<byte[]?> Read(string path, int maxBytes, CancellationToken cancellationToken)
    {
        var stream = new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.ReadWrite | FileShare.Delete,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
        });
        await using (stream.ConfigureAwait(false))
        {
            if (stream.CanSeek && stream.Length > maxBytes)
            {
                return null;
            }

            var content = new MemoryStream();
            await using (content.ConfigureAwait(false))
            {
                var buffer = new byte[BufferSize];
                int read;
                while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
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

    /// <summary>
    /// Returns <paramref name="content"/> without a leading UTF-8 byte order mark.
    /// </summary>
    public static ReadOnlyMemory<byte> WithoutUtf8ByteOrderMark(byte[] content) =>
        content.AsSpan().StartsWith(Utf8ByteOrderMark) ? content.AsMemory(Utf8ByteOrderMark.Length) : content;
}
