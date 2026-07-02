namespace SharedKernel.ImageProcessing.Tests;

internal sealed class NonSeekableStream(byte[] content) : MemoryStream(content)
{
    public override bool CanSeek => false;
}
