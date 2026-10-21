namespace PandaBear.IO;

/// <summary>Owns a bounded seekable copy of caller-owned input.</summary>
public sealed class SeekableContent : IDisposable, IAsyncDisposable
{
    private readonly Stream content;

    private SeekableContent(Stream content) => this.content = content;

    /// <summary>Gets the seekable stream, initially positioned at zero. Use sequentially and rewind between passes.</summary>
    public Stream Content => content;

    /// <summary>Copies the remaining input into memory, spilling to a private temporary file above the threshold.</summary>
    /// <param name="source">Caller-owned input, read from its current position and left open.</param>
    /// <param name="maxLength">Maximum accepted bytes, including zero for empty input only.</param>
    /// <param name="memoryThreshold">Maximum buffered content bytes in memory; zero always uses disk for nonempty content.</param>
    /// <param name="ct">Cancellation for all reads and writes.</param>
    /// <returns>A lease owning its copy, positioned at zero.</returns>
    /// <exception cref="ArgumentNullException">Input is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A limit is negative.</exception>
    /// <exception cref="ArgumentException">Input is not readable.</exception>
    /// <exception cref="InvalidDataException">Input exceeds the maximum length.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    /// <exception cref="IOException">Reading, writing, or temporary file creation fails.</exception>
    public static async ValueTask<SeekableContent> Create(Stream source, long maxLength, int memoryThreshold, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);
        ArgumentOutOfRangeException.ThrowIfNegative(memoryThreshold);
        if (!source.CanRead)
        {
            throw new ArgumentException("Input must be readable.", nameof(source));
        }

        ct.ThrowIfCancellationRequested();
        Stream destination = new MemoryStream();
        try
        {
            var buffer = new byte[81920];
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var remaining = maxLength - destination.Length;
                var requested = remaining >= buffer.Length ? buffer.Length : (int)remaining + 1;
                var read = await source.ReadAsync(buffer.AsMemory(0, requested), ct).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
                if (read == 0)
                {
                    destination.Position = 0;
                    return new SeekableContent(destination);
                }

                if (read > remaining)
                {
                    throw new InvalidDataException("Input exceeds the maximum content length.");
                }

                if (destination is MemoryStream memory)
                {
                    var length = memory.Length + read;
                    if (length > memoryThreshold)
                    {
                        var options = new FileStreamOptions
                        {
                            Mode = FileMode.CreateNew,
                            Access = FileAccess.ReadWrite,
                            Share = FileShare.None,
                            Options = FileOptions.Asynchronous | FileOptions.DeleteOnClose
                        };
                        if (!OperatingSystem.IsWindows())
                        {
                            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                        }

                        destination = new FileStream(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), options);
                        try
                        {
                            memory.Position = 0;
                            await memory.CopyToAsync(destination, ct).ConfigureAwait(false);
                        }
                        finally
                        {
                            await memory.DisposeAsync().ConfigureAwait(false);
                        }
                    }
                    else if (length > memory.Capacity)
                    {
                        memory.Capacity = (int)Math.Min(memoryThreshold, Math.Max(length, (long)memory.Capacity * 2));
                    }
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            }
        }
        catch
        {
            await destination.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Disposes the owned copy and deletes its temporary file, if any. Does not dispose input.</summary>
    public void Dispose() => content.Dispose();

    /// <summary>Asynchronously disposes the owned copy and deletes its temporary file, if any.</summary>
    /// <returns>Completion of cleanup.</returns>
    public ValueTask DisposeAsync() => content.DisposeAsync();
}
