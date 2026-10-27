namespace PandaBear.RepoConfig.Tool;

/// <summary>Reads HTTP content while enforcing a maximum size.</summary>
internal static class BoundedHttpContentReader
{
    /// <summary>Reads the content into a byte array and throws with the given message when it exceeds the limit.</summary>
    /// <param name="content">Content to read.</param>
    /// <param name="maxBytes">Maximum number of bytes allowed.</param>
    /// <param name="limitMessage">Message of the exception thrown when the limit is exceeded.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<byte[]> Read(
        HttpContent content,
        int maxBytes,
        string limitMessage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(limitMessage);

        var contentLength = content.Headers.ContentLength;
        if (contentLength.HasValue && contentLength.Value > maxBytes)
        {
            throw new InvalidOperationException(limitMessage);
        }

        var source = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (source.ConfigureAwait(false))
        {
            var destination = new MemoryStream();
            await using (destination.ConfigureAwait(false))
            {
                var buffer = new byte[8192];
                while (true)
                {
                    var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        return destination.ToArray();
                    }

                    if (destination.Length + read > maxBytes)
                    {
                        throw new InvalidOperationException(limitMessage);
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
