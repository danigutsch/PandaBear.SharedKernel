namespace PandaBear.IO.Tests;

/// <summary>A write-only stream recording the size of each write.</summary>
internal sealed class RecordingWriteStream : Stream
{
    /// <summary>Gets the size of each write in order.</summary>
    public List<int> WriteSizes { get; } = [];

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => WriteSizes.Add(count);

    public override void Write(ReadOnlySpan<byte> buffer) => WriteSizes.Add(buffer.Length);
}
