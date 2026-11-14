namespace PandaBear.ImageProcessing;

/// <summary>
/// Reads animation walk bytes from an in-memory span.
/// </summary>
/// <param name="content">The container bytes.</param>
internal readonly ref struct SpanAnimationByteSource(ReadOnlySpan<byte> content) : IAnimationByteSource
{
    private readonly ReadOnlySpan<byte> _content = content;

    /// <inheritdoc />
    public long Length => _content.Length;

    /// <inheritdoc />
    public bool TryRead(long offset, scoped Span<byte> destination)
    {
        if (offset < 0 || offset > _content.Length - (long)destination.Length)
        {
            return false;
        }

        _content.Slice((int)offset, destination.Length).CopyTo(destination);
        return true;
    }
}
