namespace PandaBear.UploadSanitizing;

/// <summary>Reads an upload from a stream into memory, never more than a limit plus one byte, so a stream that is too long is detected.</summary>
internal static class UploadReader
{
    /// <summary>The largest byte limit: one byte less than the longest byte array, so the extra byte that proves a stream too long fits.</summary>
    internal const long MaxBytesLimit = 0x7FFFFFC6;

    private const int StreamChunk = 81_920;

    private const int MaxInitialCapacity = 1024 * 1024;

    /// <summary>
    /// Reads from the current position to the end. The stream is not disposed. A seekable stream's length only sizes the first buffer,
    /// within the limit and at most 1 MiB; reads grow it from there.
    /// </summary>
    /// <param name="content">The upload.</param>
    /// <param name="maxBytes">The largest accepted length, at most <see cref="MaxBytesLimit"/>.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The bytes read, or <see langword="null"/> when the stream holds more than <paramref name="maxBytes"/>.</returns>
    internal static async Task<Memory<byte>?> Read(Stream content, long maxBytes, CancellationToken ct)
    {
        var limit = (int)maxBytes + 1;
        var buffer = new byte[InitialCapacity(content, limit)];
        var total = 0;
        while (true)
        {
            if (total == buffer.Length)
            {
                if (total == limit)
                {
                    return null;
                }

                Array.Resize(ref buffer, (int)Math.Min((long)buffer.Length * 2, limit));
            }

            var read = await content.ReadAsync(buffer.AsMemory(total), ct).ConfigureAwait(false);
            if (read == 0)
            {
                return buffer.AsMemory(0, total);
            }

            total += read;
        }
    }

    private static int InitialCapacity(Stream content, int limit)
    {
        long hint = StreamChunk;
        if (content.CanSeek)
        {
            // One more than the remaining length, so the read that returns zero needs no larger buffer. The length is only a hint: a
            // stream may report more than it holds, so it never sizes the buffer beyond MaxInitialCapacity; reads grow it from there.
            hint = Math.Max(content.Length - content.Position, 0) + 1;
        }

        return (int)Math.Clamp(hint, 1, Math.Min(limit, MaxInitialCapacity));
    }
}
