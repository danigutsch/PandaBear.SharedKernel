namespace PandaBear.IO.Tests;

/// <summary>
/// A seekable in-memory stream whose Length reports more bytes than reads return, used to test length verification.
/// </summary>
/// <param name="bytes">The bytes reads return.</param>
/// <param name="reportedLength">The Length the stream claims.</param>
internal sealed class MisreportedLengthStream(byte[] bytes, long reportedLength) : MemoryStream(bytes)
{
    public override long Length => reportedLength;
}
