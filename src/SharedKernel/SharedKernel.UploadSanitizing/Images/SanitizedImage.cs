namespace PandaBear.UploadSanitizing.Images;

/// <summary>
/// An image whose structure <see cref="ImageSanitizer"/> checked and, unless <see cref="MetadataRemoved"/> is <see langword="false"/>, whose
/// location, camera and text metadata it removed. Only the sanitizer creates instances, so holding one means the bytes passed those checks.
/// </summary>
public sealed class SanitizedImage
{
    /// <summary>Initializes a new instance of the <see cref="SanitizedImage"/> class.</summary>
    /// <param name="format">The format.</param>
    /// <param name="bytes">The sanitized bytes.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="isAnimated">Whether the image is animated.</param>
    internal SanitizedImage(ImageFormat format, ReadOnlyMemory<byte> bytes, int width, int height, bool isAnimated)
    {
        Format = format;
        Bytes = bytes;
        Width = width;
        Height = height;
        IsAnimated = isAnimated;
    }

    /// <summary>
    /// Gets a value indicating whether the metadata was removed: <see langword="true"/> for JPEG, PNG and WebP, <see langword="false"/> for
    /// AVIF, whose bytes are returned unchanged (see <see cref="ImageSanitizerOptions.AllowAvif"/>).
    /// </summary>
    public bool MetadataRemoved => Format is not ImageFormat.Avif;

    /// <summary>
    /// Gets a value indicating whether the image is animated: an APNG, an animated WebP or an AVIF image sequence. Never
    /// <see langword="true"/> when <see cref="ImageSanitizerOptions.RejectAnimation"/> is set, because animated images are refused then.
    /// </summary>
    public bool IsAnimated { get; }

    /// <summary>Gets the format recognized from the bytes.</summary>
    public ImageFormat Format { get; }

    /// <summary>Gets the image without the removed metadata; the pixel data is unchanged, nothing is re-encoded.</summary>
    public ReadOnlyMemory<byte> Bytes { get; }

    /// <summary>Gets the width in pixels, from the image header.</summary>
    public int Width { get; }

    /// <summary>Gets the height in pixels, from the image header.</summary>
    public int Height { get; }

    /// <summary>Gets the usual file extension of the format, with its dot: <c>.jpg</c>, <c>.png</c>, <c>.webp</c> or <c>.avif</c>.</summary>
    public string FileExtension => Format switch
    {
        ImageFormat.Jpeg => ".jpg",
        ImageFormat.Png => ".png",
        ImageFormat.Avif => ".avif",
        _ => ".webp",
    };

    /// <summary>Gets the media type of the format: <c>image/jpeg</c>, <c>image/png</c>, <c>image/webp</c> or <c>image/avif</c>.</summary>
    public string ContentType => Format switch
    {
        ImageFormat.Jpeg => "image/jpeg",
        ImageFormat.Png => "image/png",
        ImageFormat.Avif => "image/avif",
        _ => "image/webp",
    };
}
