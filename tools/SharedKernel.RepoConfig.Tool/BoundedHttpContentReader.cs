namespace SharedKernel.RepoConfig.Tool;

internal static class BoundedHttpContentReader
{
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
