namespace PandaBear.Testing;

/// <summary>A write-only stream recording the size of each write.</summary>
public sealed class RecordingWriteStream : Stream
{
    private readonly List<int> _writeSizes = [];

    /// <summary>Gets the size of each write in order.</summary>
    public IReadOnlyList<int> WriteSizes => _writeSizes;

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => true;

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
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => _writeSizes.Add(count);

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer) => _writeSizes.Add(buffer.Length);
}
