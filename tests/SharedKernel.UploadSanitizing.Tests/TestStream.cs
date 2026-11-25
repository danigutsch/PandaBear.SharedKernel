namespace SharedKernel.UploadSanitizing.Tests;

/// <summary>A stream over fixed bytes that counts what was read, can hide its seekability, and can report a false length.</summary>
internal sealed class TestStream : Stream
{
    private readonly byte[] _data;
    private readonly bool _seekable;
    private readonly long? _declaredLength;
    private int _position;

    /// <summary>Initializes a new instance of the <see cref="TestStream"/> class.</summary>
    /// <param name="data">The content.</param>
    /// <param name="seekable">Whether the stream reports that it can seek.</param>
    /// <param name="declaredLength">A length to report instead of the real one.</param>
    internal TestStream(byte[] data, bool seekable, long? declaredLength = null)
    {
        _data = data;
        _seekable = seekable;
        _declaredLength = declaredLength;
    }

    /// <summary>Gets the number of bytes handed out so far.</summary>
    internal int BytesRead { get; private set; }

    /// <summary>Gets a value indicating whether the stream was disposed.</summary>
    internal bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => _seekable;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => _seekable ? _declaredLength ?? _data.Length : throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => _seekable ? _position : throw new NotSupportedException();
        set => _position = _seekable ? (int)value : throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        var count = Math.Min(buffer.Length, _data.Length - _position);
        _data.AsSpan(_position, count).CopyTo(buffer);
        _position += count;
        BytesRead += count;
        return count;
    }

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Read(buffer.Span));
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override void Flush()
    {
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
        IsDisposed = true;
        base.Dispose(disposing);
    }
}
