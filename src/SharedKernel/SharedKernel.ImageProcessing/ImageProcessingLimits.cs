namespace SharedKernel.ImageProcessing;

/// <summary>
/// Decoded image limits enforced before variants are generated.
/// </summary>
/// <param name="MaxWidth">The maximum decoded width in pixels.</param>
/// <param name="MaxHeight">The maximum decoded height in pixels.</param>
/// <param name="MaxPixelCount">The maximum decoded pixel count.</param>
public sealed record ImageProcessingLimits(int MaxWidth, int MaxHeight, long MaxPixelCount)
{
    /// <summary>
    /// Default limits for ordinary web images.
    /// </summary>
    public static ImageProcessingLimits WebDefault { get; } = new(8_000, 8_000, 40_000_000);
}
