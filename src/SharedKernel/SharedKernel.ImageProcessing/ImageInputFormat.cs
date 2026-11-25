namespace SharedKernel.ImageProcessing;

/// <summary>
/// Decoded image formats accepted as processing input.
/// </summary>
public enum ImageInputFormat
{
    /// <summary>JPEG image format.</summary>
    Jpeg,

    /// <summary>PNG image format, including animated PNG.</summary>
    Png,

    /// <summary>WebP image format.</summary>
    WebP,

    /// <summary>AV1 Image File Format, including AVIF image sequences.</summary>
    Avif
}
