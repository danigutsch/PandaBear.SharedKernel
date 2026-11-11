namespace PandaBear.ImageSanitizing;

/// <summary>
/// An image whose structure <see cref="ImageSanitizer"/> checked and whose location, camera and text metadata it removed. Only the sanitizer
/// creates instances, so holding one means the bytes passed those checks.
/// </summary>
public sealed class SanitizedImage
{
    /// <summary>Initializes a new instance of the <see cref="SanitizedImage"/> class.</summary>
    /// <param name="format">The format.</param>
    /// <param name="bytes">The sanitized bytes.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    internal SanitizedImage(ImageFormat format, ReadOnlyMemory<byte> bytes, int width, int height)
    {
        Format = format;
        Bytes = bytes;
        Width = width;
        Height = height;
    }

    /// <summary>Gets the format recognized from the bytes.</summary>
    public ImageFormat Format
    {
        get;
    }

    /// <summary>Gets the image without the removed metadata; the pixel data is unchanged, nothing is re-encoded.</summary>
    public ReadOnlyMemory<byte> Bytes
    {
        get;
    }

    /// <summary>Gets the width in pixels, from the image header.</summary>
    public int Width
    {
        get;
    }

    /// <summary>Gets the height in pixels, from the image header.</summary>
    public int Height
    {
        get;
    }

    /// <summary>Gets the usual file extension of the format, with its dot: <c>.jpg</c>, <c>.png</c> or <c>.webp</c>.</summary>
    public string FileExtension => Format switch
    {
        ImageFormat.Jpeg => ".jpg",
        ImageFormat.Png => ".png",
        _ => ".webp",
    };

    /// <summary>Gets the media type of the format: <c>image/jpeg</c>, <c>image/png</c> or <c>image/webp</c>.</summary>
    public string ContentType => Format switch
    {
        ImageFormat.Jpeg => "image/jpeg",
        ImageFormat.Png => "image/png",
        _ => "image/webp",
    };
}
