namespace PandaBear.Secrets.Tool;

/// <summary>
/// Reads a file's bytes with a size limit and a time limit. A FIFO or a device blocks on open or never ends, so the read runs on its own thread and is
/// abandoned when it does not finish in time; the file then counts as not readable instead of hanging the tool.
/// </summary>
internal static class GuardedFileReader
{
    /// <summary>The default time limit for one read.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Reads at most <paramref name="maxBytes"/> bytes; returns the bytes, or a problem that says why they cannot be had.</summary>
    public static async Task<(byte[] Bytes, string? Problem)> Read(string path, long maxBytes, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await Task.Run(() => ReadBounded(path, maxBytes), cancellationToken).WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            return bytes is null ? ([], $"the file is larger than {maxBytes / (1024 * 1024)} MiB") : (bytes, null);
        }
        catch (TimeoutException)
        {
            return ([], "the file cannot be read: it did not finish reading (is it a FIFO or a device?)");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ([], "the file cannot be read");
        }
    }

    private static byte[]? ReadBounded(string path, long maxBytes)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.None);
        using MemoryStream collected = new();
        var chunk = new byte[8192];
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            collected.Write(chunk, 0, read);
            if (collected.Length > maxBytes)
            {
                return null;
            }
        }

        return collected.ToArray();
    }
}
