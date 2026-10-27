namespace PandaBear.IO.Tests;

/// <summary>
/// A readable in-memory stream that reports CanSeek as false and throws NotSupportedException for Length and Position.
/// </summary>
/// /// <param name="bytes">The bytes the stream exposes.</param>
internal sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
{
    public override bool CanSeek => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }
}
