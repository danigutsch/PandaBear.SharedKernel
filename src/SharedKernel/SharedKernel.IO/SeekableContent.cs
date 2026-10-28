using System.Buffers;

namespace PandaBear.IO;

/// <summary>Leases bounded seekable content: a private copy, or a caller-owned seekable source passed through uncopied.</summary>
public sealed class SeekableContent : IDisposable, IAsyncDisposable
{
    private readonly Stream content;
    private readonly bool ownsContent;

    private SeekableContent(Stream content, bool ownsContent)
    {
        this.content = content;
        this.ownsContent = ownsContent;
    }

    /// <summary>
    /// Gets the seekable stream, initially positioned at zero, whose Length is the content length. Use sequentially and
    /// rewind between passes. When the input was passed through, this is the caller's own stream: its Position is shared
    /// mutable state, so do not use the source concurrently or after the lease lifetime.
    /// </summary>
    public Stream Content => content;

    /// <summary>
    /// Passes a seekable input at position zero through after one bounded counting read pass; otherwise copies the
    /// remaining input into memory, spilling to a private temporary file above the threshold.
    /// </summary>
    /// <param name="source">Caller-owned input, read from its current position and left open.</param>
    /// <param name="maxLength">Maximum accepted bytes, including zero for empty input only.</param>
    /// <param name="memoryThreshold">Maximum buffered content bytes in memory for a copy; zero always uses disk for nonempty copies. Ignored for pass-through.</param>
    /// <param name="ct">Cancellation for all reads and writes.</param>
    /// <returns>
    /// A lease positioned at zero. It owns its copy, or, for a seekable input at position zero, merely exposes the input,
    /// which it never disposes. A failed or canceled pass-through restores the input to position zero.
    /// </returns>
    /// <exception cref="ArgumentNullException">Input is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A limit is negative.</exception>
    /// <exception cref="ArgumentException">Input is not readable.</exception>
    /// <exception cref="InvalidDataException">Input exceeds the maximum length, or a seekable input's Length disagrees with the bytes read.</exception>
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
        if (source.CanSeek && source.Position == 0)
        {
            return await PassThrough(source, maxLength, ct).ConfigureAwait(false);
        }

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
                    return new SeekableContent(destination, ownsContent: true);
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

    /// <summary>Disposes the owned copy and deletes its temporary file, if any. Never disposes input, passed through or not.</summary>
    public void Dispose()
    {
        if (ownsContent)
        {
            content.Dispose();
        }
    }

    /// <summary>Asynchronously disposes the owned copy and deletes its temporary file, if any. Never disposes input.</summary>
    /// <returns>Completion of cleanup.</returns>
    public ValueTask DisposeAsync() => ownsContent ? content.DisposeAsync() : ValueTask.CompletedTask;

    private static async ValueTask<SeekableContent> PassThrough(Stream source, long maxLength, CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            long total = 0;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var remaining = maxLength - total;
                var requested = remaining >= buffer.Length ? buffer.Length : (int)remaining + 1;
                var read = await source.ReadAsync(buffer.AsMemory(0, requested), ct).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
                if (read == 0)
                {
                    break;
                }

                if (read > remaining)
                {
                    throw new InvalidDataException("Input exceeds the maximum content length.");
                }

                total += read;
            }

            if (total != source.Length)
            {
                throw new InvalidDataException("Seekable input length does not match the bytes read.");
            }

            source.Position = 0;
            return new SeekableContent(source, ownsContent: false);
        }
        catch
        {
            try
            {
                source.Position = 0;
            }
            catch (Exception restoreFailure) when (restoreFailure is IOException or NotSupportedException or ObjectDisposedException)
            {
                // The original failure is rethrown; a stream that cannot rewind is the caller's to discard.
            }

            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
