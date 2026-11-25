using System.Diagnostics.CodeAnalysis;

namespace SharedKernel.Testing;

/// <summary>
/// Non-seekable, read-only stream that serves a prefix, then blocks the next read until the test calls <see cref="Release" />, the
/// read's cancellation token fires, or the stream is disposed; after release it serves the remainder and then reports the end.
/// Used to test cancellation, partial copies and disposal while a body is being read.
/// </summary>
/// <remarks>
/// Asynchronous reads honour their cancellation token while blocked; synchronous reads block until <see cref="Release" /> or disposal. A
/// blocked read that the stream's disposal ends throws <see cref="ObjectDisposedException" />. <c>CopyTo</c> and <c>CopyToAsync</c> write the
/// prefix to the destination before they block.
/// </remarks>
public sealed class BlockingReadStream : Stream
{
    private readonly TaskCompletionSource _readStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _blocked = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock _gate = new();
    private readonly byte[] _prefix;
    private readonly byte[] _remainder;
    private Action? _onReadStarted;
    private long _bytesRead;
    private int _blockedReadCancelled;

    /// <summary>Initializes a stream without content whose first read blocks until it is released, cancelled or disposed.</summary>
    /// <param name="onReadStarted">An optional callback invoked once when the first read begins; an exception it throws propagates to the reader after <see cref="ReadStarted" /> completes.</param>
    [SuppressMessage("API Design", "RS0027:Public API with optional parameter(s) should have the most parameters", Justification = "The shipped constructor keeps its optional callback; the content overload is additive.")]
    public BlockingReadStream(Action? onReadStarted = null)
    {
        _prefix = [];
        _remainder = [];
        _onReadStarted = onReadStarted;
    }

    /// <summary>Initializes a stream that serves <paramref name="prefix" />, blocks, and serves <paramref name="remainder" /> after <see cref="Release" />.</summary>
    /// <param name="prefix">The bytes served before the stream blocks; empty to block on the first read.</param>
    /// <param name="remainder">The bytes served after release; empty to report the end right after release.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="prefix" /> or <paramref name="remainder" /> is <see langword="null" />.</exception>
    public BlockingReadStream(byte[] prefix, byte[] remainder)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(remainder);
        _prefix = prefix;
        _remainder = remainder;
    }

    /// <summary>Gets a value indicating whether the stream has been disposed.</summary>
    public bool IsDisposed => _disposed.Task.IsCompleted;

    /// <summary>Gets a task that completes when the stream is disposed.</summary>
    public Task Disposed => _disposed.Task;

    /// <summary>Gets a task that completes when the first read begins.</summary>
    public Task ReadStarted => _readStarted.Task;

    /// <summary>Gets a task that completes when a read has consumed the prefix and is blocked waiting for <see cref="Release" />.</summary>
    public Task Blocked => _blocked.Task;

    /// <summary>Gets a value indicating whether a blocked read ended because its cancellation token fired.</summary>
    public bool BlockedReadCancelled => Volatile.Read(ref _blockedReadCancelled) != 0;

    /// <summary>Gets the total number of bytes returned to readers.</summary>
    public long BytesRead => Interlocked.Read(ref _bytesRead);

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

    /// <summary>Lets a blocked read, and every later read, continue with the remainder.</summary>
    public void Release() => _released.TrySetResult();

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
        BeginRead();
        var served = TryServe(buffer);
        if (served >= 0)
        {
            return served;
        }

        _blocked.TrySetResult();
        Task.WaitAny(_released.Task, _disposed.Task);
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return TryServe(buffer);
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        ValidateBufferArguments(buffer, offset, count);
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        BeginRead();
        var served = TryServe(buffer.Span);
        if (served >= 0)
        {
            return served;
        }

        _blocked.TrySetResult();
        try
        {
            await Task.WhenAny(_released.Task, _disposed.Task).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Volatile.Write(ref _blockedReadCancelled, 1);
            throw;
        }

        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return TryServe(buffer.Span);
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
            _disposed.TrySetResult();
        }

        base.Dispose(disposing);
    }

    private void BeginRead()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        try
        {
            Interlocked.Exchange(ref _onReadStarted, null)?.Invoke();
        }
        finally
        {
            _readStarted.TrySetResult();
        }
    }

    private int TryServe(Span<byte> buffer)
    {
        lock (_gate)
        {
            var position = _bytesRead;
            if (position < _prefix.Length)
            {
                return Copy(_prefix.AsSpan((int)position), buffer);
            }

            if (!_released.Task.IsCompleted)
            {
                return -1;
            }

            var remainderPosition = (int)(position - _prefix.Length);
            return Copy(_remainder.AsSpan(remainderPosition), buffer);
        }
    }

    private int Copy(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        var count = Math.Min(source.Length, destination.Length);
        source[..count].CopyTo(destination);
        Interlocked.Add(ref _bytesRead, count);
        return count;
    }
}
