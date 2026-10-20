using System.Diagnostics.CodeAnalysis;

namespace SharedKernel.Testing;

/// <summary>
/// Provides a deterministic read barrier and byte count for cancellation and bounded-stream tests.
/// </summary>
public sealed class GatedCountingReadStream : Stream
{
    private readonly MemoryStream _inner;
    private readonly TaskCompletionSource _readGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _readStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Initializes the stream with test-owned content.
    /// </summary>
    /// <param name="content">The bytes exposed after the read gate opens.</param>
    public GatedCountingReadStream(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        _inner = new MemoryStream(content, writable: false);
    }

    /// <summary>
    /// Gets a task that completes when a reader reaches the gate.
    /// </summary>
    public Task ReadStarted => _readStarted.Task;

    /// <summary>
    /// Gets the cumulative number of bytes returned to readers.
    /// </summary>
    public long BytesRead { get; private set; }

    /// <summary>
    /// Opens the read gate.
    /// </summary>
    public void Release() => _readGate.TrySetResult();

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => true;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => _inner.Length;

    /// <inheritdoc />
    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        _readStarted.TrySetResult();
        if (!_readGate.Task.IsCompletedSuccessfully)
        {
            throw new InvalidOperationException("Release the read gate before using synchronous reads.");
        }

        var bytesRead = _inner.Read(buffer, offset, count);
        BytesRead += bytesRead;
        return bytesRead;
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return ReadCore(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    /// <inheritdoc />
    [SuppressMessage("API Design", "RS0027:Public API with optional parameter(s) should have the most parameters", Justification = "The override preserves the inherited Stream signature without redeclaring optional metadata.")]
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        ReadCore(buffer, cancellationToken);

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }

    private async ValueTask<int> ReadCore(Memory<byte> buffer, CancellationToken ct)
    {
        _readStarted.TrySetResult();
        await _readGate.Task.WaitAsync(ct).ConfigureAwait(false);
        var bytesRead = await _inner.ReadAsync(buffer, ct).ConfigureAwait(false);
        BytesRead += bytesRead;
        return bytesRead;
    }
}
