namespace SharedKernel.Testing;

/// <summary>Observes requested and returned sizes of asynchronous reads from a borrowed stream.</summary>
/// <remarks>
/// The wrapper is non-seekable and read-only. Synchronous reads are unsupported. Faulted and cancelled reads do not notify
/// the observer. Disposing the wrapper leaves the inner stream open.
/// </remarks>
/// <param name="read">The asynchronous read operation of the stream whose lifetime the caller owns.</param>
/// <param name="observeRead">Receives the requested buffer size and returned byte count, including zero at end of stream.</param>
/// <exception cref="ArgumentNullException"><paramref name="read"/> or <paramref name="observeRead"/> is null.</exception>
public sealed class ReadObservingStream(
    Func<Memory<byte>, CancellationToken, ValueTask<int>> read,
    Action<int, int> observeRead) : Stream
{
    private readonly Func<Memory<byte>, CancellationToken, ValueTask<int>> _read = read ?? throw new ArgumentNullException(nameof(read));
    private readonly Action<int, int> _observeRead = observeRead ?? throw new ArgumentNullException(nameof(observeRead));
    private bool _disposed;

    /// <inheritdoc />
    public override bool CanRead => !_disposed;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var count = await _read(buffer, cancellationToken).ConfigureAwait(false);
        _observeRead(buffer.Length, count);
        return count;
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBufferArguments(buffer, offset, count);
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override int Read(Span<byte> buffer) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Flush() => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _disposed = true;
        base.Dispose(disposing);
    }
}
