namespace PandaBear.Testing;

/// <summary>
/// Stream wrapper that delegates every operation to an inner stream and records how it was disposed, so a test can prove that code
/// disposes a stream it was handed (or let it wait until it does).
/// </summary>
/// <remarks>
/// The first disposal, through <see cref="Stream.Dispose()" /> or <see cref="DisposeAsync" />, disposes the inner stream the same way and
/// completes <see cref="Disposed" /> even when the inner disposal throws. Later disposals are counted but do not touch the inner stream.
/// Compose it with another stream double, for example <see cref="BlockingReadStream" />, to observe disposal after a stalled read.
/// </remarks>
/// <param name="inner">The stream to delegate to; it is disposed with the wrapper.</param>
public sealed class DisposalTrackingStream(Stream inner) : Stream
{
    private readonly Stream _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly TaskCompletionSource _disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposeCount;
    private int _asyncDisposeCount;
    private int _innerDisposed;
    private bool _completingAsyncDispose;

    /// <summary>Gets the wrapped stream.</summary>
    public Stream InnerStream => _inner;

    /// <summary>Gets a value indicating whether the stream has been disposed.</summary>
    public bool IsDisposed => _disposed.Task.IsCompleted;

    /// <summary>Gets a task that completes when the stream is first disposed.</summary>
    public Task Disposed => _disposed.Task;

    /// <summary>Gets the number of synchronous disposals (<see cref="Stream.Dispose()" /> or <see cref="Stream.Close" />).</summary>
    public int DisposeCount => Volatile.Read(ref _disposeCount);

    /// <summary>Gets the number of <see cref="DisposeAsync" /> calls.</summary>
    public int AsyncDisposeCount => Volatile.Read(ref _asyncDisposeCount);

    /// <inheritdoc />
    public override bool CanRead => !IsDisposed && _inner.CanRead;

    /// <inheritdoc />
    public override bool CanSeek => !IsDisposed && _inner.CanSeek;

    /// <inheritdoc />
    public override bool CanWrite => !IsDisposed && _inner.CanWrite;

    /// <inheritdoc />
    public override long Length => _inner.Length;

    /// <inheritdoc />
    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    /// <inheritdoc />
    public override void Flush() => _inner.Flush();

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

    /// <inheritdoc />
    public override int Read(Span<byte> buffer) => _inner.Read(buffer);

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        _inner.ReadAsync(buffer, offset, count, cancellationToken);

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        _inner.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

    /// <inheritdoc />
    public override void SetLength(long value) => _inner.SetLength(value);

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer) => _inner.Write(buffer);

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        _inner.WriteAsync(buffer, offset, count, cancellationToken);

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        _inner.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref _asyncDisposeCount);
        if (Interlocked.Exchange(ref _innerDisposed, 1) == 0)
        {
            try
            {
                await _inner.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                _disposed.TrySetResult();
            }
        }

        _completingAsyncDispose = true;
        try
        {
            await base.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _completingAsyncDispose = false;
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_completingAsyncDispose)
        {
            Interlocked.Increment(ref _disposeCount);
            if (Interlocked.Exchange(ref _innerDisposed, 1) == 0)
            {
                try
                {
                    _inner.Dispose();
                }
                finally
                {
                    _disposed.TrySetResult();
                }
            }
        }

        base.Dispose(disposing);
    }
}
