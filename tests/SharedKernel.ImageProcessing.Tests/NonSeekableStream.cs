namespace PandaBear.ImageProcessing.Tests;

/// <summary>
/// A readable in-memory stream that reports CanSeek as false, used to check that the processor requires seekable input.
/// </summary>
/// /// <param name="content">The bytes the stream exposes.</param>
internal sealed class NonSeekableStream(byte[] content) : MemoryStream(content)
{
    public override bool CanSeek => false;
}
