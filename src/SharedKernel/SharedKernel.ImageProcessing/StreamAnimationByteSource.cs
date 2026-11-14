namespace PandaBear.ImageProcessing;

/// <summary>
/// Reads animation walk bytes from a seekable stream, relative to the position the walk started at.
/// </summary>
/// <param name="stream">The seekable container stream.</param>
/// <param name="origin">The stream position that the walk treats as offset zero.</param>
internal readonly struct StreamAnimationByteSource(Stream stream, long origin) : IAnimationByteSource
{
    /// <inheritdoc />
    public long Length { get; } = Math.Max(0, stream.Length - origin);

    /// <inheritdoc />
    public bool TryRead(long offset, scoped Span<byte> destination)
    {
        if (offset < 0 || offset > Length - destination.Length)
        {
            return false;
        }

        stream.Position = origin + offset;
        return stream.ReadAtLeast(destination, destination.Length, throwOnEndOfStream: false) == destination.Length;
    }
}
