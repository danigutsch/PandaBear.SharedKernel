namespace PandaBear.IO;

/// <summary>
/// A read-only, seekable stream over a <see cref="ReadOnlyMemory{T}"/> of bytes that never copies the memory.
/// </summary>
/// <remarks>
/// Works with any memory, including memory that is not backed by an array. The stream does not own the memory;
/// callers must keep it valid and unchanged while the stream is in use. Use sequentially and rewind with
/// <see cref="Stream.Position"/> for another pass. Cancellation is checked before each asynchronous operation
/// and a canceled token surfaces as a <see cref="TaskCanceledException"/>, which is an <see cref="OperationCanceledException"/>; reads and copies otherwise complete synchronously.
/// </remarks>
/// <param name="memory">The bytes to expose, positioned at zero. They must stay valid and unchanged while the stream is used.</param>
public sealed class ReadOnlyMemoryStream(ReadOnlyMemory<byte> memory) : Stream
{
    private ReadOnlyMemory<byte> memory = memory;
    private long position;
    private bool disposed;

    /// <summary>Gets a value indicating whether the stream can be read, which is true until disposal.</summary>
    public override bool CanRead => !disposed;

    /// <summary>Gets a value indicating whether the stream can seek, which is true until disposal.</summary>
    public override bool CanSeek => !disposed;

    /// <summary>Gets a value indicating whether the stream can be written, which is always false.</summary>
    public override bool CanWrite => false;

    /// <summary>Gets the number of bytes in the underlying memory.</summary>
    /// <exception cref="ObjectDisposedException">The stream is disposed.</exception>
    public override long Length
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return memory.Length;
        }
    }

    /// <summary>Gets or sets the current position. Positions beyond the end are allowed and read nothing.</summary>
    /// <exception cref="ObjectDisposedException">The stream is disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public override long Position
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return position;
        }
        set
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            position = value;
        }
    }

    /// <summary>Does nothing because the stream is read-only.</summary>
    public override void Flush()
    {
    }

    /// <summary>Reads up to <paramref name="count"/> bytes into <paramref name="buffer"/>.</summary>
    /// <param name="buffer">The destination array.</param>
    /// <param name="offset">The index in <paramref name="buffer"/> at which to start storing bytes.</param>
    /// <param name="count">The maximum number of bytes to read.</param>
    /// <returns>The number of bytes read, or zero at or beyond the end.</returns>
    /// <exception cref="ObjectDisposedException">The stream is disposed.</exception>
    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        return Read(buffer.AsSpan(offset, count));
    }

    /// <summary>Reads up to <c>buffer.Length</c> bytes into <paramref name="buffer"/>.</summary>
    /// <param name="buffer">The destination span.</param>
    /// <returns>The number of bytes read, or zero at or beyond the end.</returns>
    /// <exception cref="ObjectDisposedException">The stream is disposed.</exception>
    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (position >= memory.Length)
        {
            return 0;
        }

        var start = (int)position;
        var read = Math.Min(buffer.Length, memory.Length - start);
        memory.Span.Slice(start, read).CopyTo(buffer);
        position += read;
        return read;
    }

    /// <summary>Reads one byte.</summary>
    /// <returns>The byte, or -1 at or beyond the end.</returns>
    /// <exception cref="ObjectDisposedException">The stream is disposed.</exception>
    public override int ReadByte()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (position >= memory.Length)
        {
            return -1;
        }

        return memory.Span[(int)position++];
    }

    /// <summary>Reads up to <paramref name="count"/> bytes; the returned task is already completed unless canceled.</summary>
    /// <param name="buffer">The destination array.</param>
    /// <param name="offset">The index in <paramref name="buffer"/> at which to start storing bytes.</param>
    /// <param name="count">The maximum number of bytes to read.</param>
    /// <param name="cancellationToken">A token checked before reading.</param>
    /// <returns>The number of bytes read, or zero at or beyond the end.</returns>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    /// <exception cref="ObjectDisposedException">The stream is disposed.</exception>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBufferArguments(buffer, offset, count);
        return cancellationToken.IsCancellationRequested
            ? Task.FromCanceled<int>(cancellationToken)
            : Task.FromResult(Read(buffer.AsSpan(offset, count)));
    }

    /// <summary>Reads up to <c>buffer.Length</c> bytes; the returned value task is already completed unless canceled.</summary>
    /// <param name="buffer">The destination memory.</param>
    /// <param name="cancellationToken">A token checked before reading.</param>
    /// <returns>The number of bytes read, or zero at or beyond the end.</returns>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    /// <exception cref="ObjectDisposedException">The stream is disposed.</exception>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        cancellationToken.IsCancellationRequested
            ? ValueTask.FromCanceled<int>(cancellationToken)
            : new ValueTask<int>(Read(buffer.Span));

    /// <summary>Writes all remaining bytes to <paramref name="destination"/> in a single write.</summary>
    /// <param name="destination">The stream to write to.</param>
    /// <param name="bufferSize">Ignored except for validation; the remaining memory is written at once.</param>
    /// <exception cref="ObjectDisposedException">The stream is disposed.</exception>
    public override void CopyTo(Stream destination, int bufferSize)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ValidateCopyToArguments(destination, bufferSize);
        destination.Write(TakeRemaining().Span);
    }

    /// <summary>Writes all remaining bytes to <paramref name="destination"/> in a single write.</summary>
    /// <param name="destination">The stream to write to.</param>
    /// <param name="bufferSize">Ignored except for validation; the remaining memory is written at once.</param>
    /// <param name="cancellationToken">A token checked before and passed to the write.</param>
    /// <returns>A task that completes when the write completes.</returns>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    /// <exception cref="ObjectDisposedException">The stream is disposed.</exception>
    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ValidateCopyToArguments(destination, bufferSize);
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        return destination.WriteAsync(TakeRemaining(), cancellationToken).AsTask();
    }

    /// <summary>Sets the position.</summary>
    /// <param name="offset">The offset relative to <paramref name="origin"/>.</param>
    /// <param name="origin">The reference point.</param>
    /// <returns>The new position.</returns>
    /// <exception cref="ObjectDisposedException">The stream is disposed.</exception>
    /// <exception cref="ArgumentException">The origin is not a defined value.</exception>
    /// <exception cref="IOException">The resulting position is negative or too large.</exception>
    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var reference = origin switch
        {
            SeekOrigin.Begin => 0,
            SeekOrigin.Current => position,
            SeekOrigin.End => memory.Length,
            _ => throw new ArgumentException("Origin is not a valid SeekOrigin.", nameof(origin)),
        };

        if (offset > long.MaxValue - reference || reference + offset < 0)
        {
            throw new IOException("An attempt was made to move the position before the beginning or past the maximum length.");
        }

        position = reference + offset;
        return position;
    }

    /// <summary>Not supported because the stream is read-only.</summary>
    /// <param name="value">Ignored.</param>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public override void SetLength(long value) => throw new NotSupportedException("The stream is read-only.");

    /// <summary>Not supported because the stream is read-only.</summary>
    /// <param name="buffer">Ignored.</param>
    /// <param name="offset">Ignored.</param>
    /// <param name="count">Ignored.</param>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("The stream is read-only.");

    /// <summary>Not supported because the stream is read-only.</summary>
    /// <param name="buffer">Ignored.</param>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public override void Write(ReadOnlySpan<byte> buffer) => throw new NotSupportedException("The stream is read-only.");

    /// <summary>Marks the stream disposed and releases its reference to the memory. Disposing again does nothing.</summary>
    /// <param name="disposing">True when called from <see cref="Stream.Dispose()"/>.</param>
    protected override void Dispose(bool disposing)
    {
        disposed = true;
        memory = default;
        base.Dispose(disposing);
    }

    private ReadOnlyMemory<byte> TakeRemaining()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (position >= memory.Length)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        var remaining = memory[(int)position..];
        position = memory.Length;
        return remaining;
    }
}
