namespace PandaBear.ImageProcessing;

/// <summary>
/// Supported encoded image output formats.
/// </summary>
public enum ImageOutputFormat
{
    /// <summary>AV1 Image File Format.</summary>
    Avif,

    /// <summary>WebP image format.</summary>
    WebP,

    /// <summary>JPEG image format.</summary>
    Jpeg,

    /// <summary>Windows icon image format.</summary>
    Ico,

    /// <summary>
    /// Lossless PNG image format. Images with alpha are written as 8-bit-per-channel RGBA, and no text, time, or EXIF
    /// chunks are written. The variant quality is still validated but does not affect lossless PNG output.
    /// </summary>
    Png
}
