namespace SharedKernel.Testing;

/// <summary>
/// Non-seekable, read-only stream that serves its content in chunks of at most a fixed size and holds every read until the test releases
/// it, so a test can observe a reader between chunks.
/// </summary>
/// <remarks>
/// Releases are counted: each <see cref="ReleaseRead" /> lets exactly one current or future read through, and <see cref="ReleaseAll" />
/// opens the gate for good. Every read, including one that reports the end, needs a release. Asynchronous reads honour their cancellation
/// token while held; synchronous reads wait until a release or disposal. Disposal ends held reads with
/// <see cref="ObjectDisposedException" />. Reads must not overlap, as with any stream.
/// </remarks>
public sealed class GatedChunkReadStream : Stream
{
    private readonly byte[] _content;
    private readonly int _chunkSize;
    private readonly SemaphoreSlim _permits = new(0);
    private readonly SemaphoreSlim _pending = new(0);
    private readonly Lock _gate = new();
    private int _open;
    private int _disposed;
    private int _offset;
    private int _readCount;
    private int _largestRequestedRead;

    /// <summary>Initializes the stream with test-owned content.</summary>
    /// <param name="content">The bytes to serve.</param>
    /// <param name="chunkSize">The most bytes one read returns.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="content" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="chunkSize" /> is less than one.</exception>
    public GatedChunkReadStream(byte[] content, int chunkSize)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);
        _content = content;
        _chunkSize = chunkSize;
    }

    /// <summary>Gets a value indicating whether the stream has been disposed.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>Gets the total number of bytes returned to readers.</summary>
    public long BytesRead
    {
        get
        {
            lock (_gate)
            {
                return _offset;
            }
        }
    }

    /// <summary>Gets the number of reads started, including reads still held and reads that reported the end.</summary>
    public int ReadCount => Volatile.Read(ref _readCount);

    /// <summary>Gets the largest buffer any read requested, including reads that returned no data.</summary>
    public int LargestRequestedRead => Volatile.Read(ref _largestRequestedRead);

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

    /// <summary>Completes once the next read has started and is held (or passed the gate when it is open); each started read completes one call.</summary>
    /// <param name="ct">The token that cancels the wait.</param>
    /// <returns>A task that completes when a read has started.</returns>
    public Task WaitForPendingRead(CancellationToken ct) => _pending.WaitAsync(ct);

    /// <summary>Lets exactly one current or future read continue.</summary>
    public void ReleaseRead()
    {
        if (Volatile.Read(ref _open) == 0)
        {
            _permits.Release();
        }
    }

    /// <summary>Lets every current and future read continue.</summary>
    public void ReleaseAll() => Open();

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
        BeginRead(buffer.Length);
        if (Volatile.Read(ref _open) == 0)
        {
            _permits.Wait();
        }

        return Serve(buffer);
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBufferArguments(buffer, offset, count);
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        BeginRead(buffer.Length);
        if (Volatile.Read(ref _open) == 0)
        {
            await _permits.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Serve(buffer.Span);
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
        if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            Open();
            _permits.Dispose();
            _pending.Dispose();
        }

        base.Dispose(disposing);
    }

    private void Open()
    {
        if (Interlocked.Exchange(ref _open, 1) == 0)
        {
            _permits.Release(int.MaxValue / 2);
        }
    }

    private void BeginRead(int requested)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        Interlocked.Increment(ref _readCount);
        int largest;
        do
        {
            largest = Volatile.Read(ref _largestRequestedRead);
        }
        while (requested > largest && Interlocked.CompareExchange(ref _largestRequestedRead, requested, largest) != largest);

        _pending.Release();
    }

    private int Serve(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        lock (_gate)
        {
            var count = Math.Min(Math.Min(_chunkSize, buffer.Length), _content.Length - _offset);
            _content.AsSpan(_offset, count).CopyTo(buffer);
            _offset += count;
            return count;
        }
    }
}
