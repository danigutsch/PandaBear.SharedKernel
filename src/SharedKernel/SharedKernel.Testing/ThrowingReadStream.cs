namespace SharedKernel.Testing;

/// <summary>
/// Non-seekable, read-only stream that serves a prefix and then throws a chosen exception from every later read, used to test how a
/// reader handles a body that fails part way.
/// </summary>
/// <remarks>
/// Every read after the prefix throws the same exception instance. Asynchronous reads return a faulted task rather than throwing
/// synchronously, and report a cancelled token before the failure. <c>CopyTo</c> and <c>CopyToAsync</c> write the prefix to the
/// destination before they throw.
/// </remarks>
public sealed class ThrowingReadStream : Stream
{
    private readonly byte[] _prefix;
    private readonly Exception _exception;
    private readonly Lock _gate = new();
    private int _served;
    private int _readCount;
    private int _disposed;

    /// <summary>Initializes a stream whose every read throws <paramref name="exception" />.</summary>
    /// <param name="exception">The exception every read throws.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="exception" /> is <see langword="null" />.</exception>
    public ThrowingReadStream(Exception exception)
        : this([], exception)
    {
    }

    /// <summary>Initializes a stream that serves <paramref name="prefix" /> and then throws <paramref name="exception" />.</summary>
    /// <param name="prefix">The bytes served before the failure.</param>
    /// <param name="exception">The exception every read after the prefix throws.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="prefix" /> or <paramref name="exception" /> is <see langword="null" />.</exception>
    public ThrowingReadStream(byte[] prefix, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(exception);
        _prefix = prefix;
        _exception = exception;
    }

    /// <summary>Gets a value indicating whether the stream has been disposed.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>Gets the number of read attempts, including those that threw.</summary>
    public int ReadCount => Volatile.Read(ref _readCount);

    /// <summary>Gets the number of prefix bytes returned to readers.</summary>
    public long BytesRead
    {
        get
        {
            lock (_gate)
            {
                return _served;
            }
        }
    }

    /// <inheritdoc />
    public override bool CanRead => !IsDisposed;

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
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        return Read(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        Interlocked.Increment(ref _readCount);
        var served = TryServe(buffer);
        return served >= 0 ? served : throw _exception;
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBufferArguments(buffer, offset, count);
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<int>(cancellationToken);
        }

        Interlocked.Increment(ref _readCount);
        var served = TryServe(buffer.Span);
        return served >= 0 ? ValueTask.FromResult(served) : ValueTask.FromException<int>(_exception);
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Volatile.Write(ref _disposed, 1);
        }

        base.Dispose(disposing);
    }

    private int TryServe(Span<byte> buffer)
    {
        lock (_gate)
        {
            if (_served >= _prefix.Length)
            {
                return -1;
            }

            var count = Math.Min(buffer.Length, _prefix.Length - _served);
            _prefix.AsSpan(_served, count).CopyTo(buffer);
            _served += count;
            return count;
        }
    }
}
