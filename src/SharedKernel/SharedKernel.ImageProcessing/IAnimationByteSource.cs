namespace PandaBear.ImageProcessing;

/// <summary>
/// Random-access reads over the bytes the animation walk inspects.
/// </summary>
internal interface IAnimationByteSource
{
    /// <summary>
    /// Gets the number of bytes available to the walk.
    /// </summary>
    long Length
    {
        get;
    }

    /// <summary>
    /// Fills <paramref name="destination" /> from <paramref name="offset" />, or returns <see langword="false" /> when the range is not fully available.
    /// </summary>
    bool TryRead(long offset, scoped Span<byte> destination);
}
