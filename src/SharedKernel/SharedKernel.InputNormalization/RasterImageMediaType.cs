// Adapted from ViajantesTurismo. See NOTICE.ViajantesTurismo.txt for the MIT license and attribution.

namespace PandaBear.InputNormalization;

/// <summary>A canonical media type and extension for a raster image rendition.</summary>
public sealed class RasterImageMediaType
{
    private static readonly RasterImageMediaType[] Formats =
    [
        new("image/avif", "avif"),
        new("image/jpeg", "jpg"),
        new("image/png", "png"),
        new("image/webp", "webp"),
    ];

    private RasterImageMediaType(string mediaType, string fileExtension)
    {
        MediaType = mediaType;
        FileExtension = fileExtension;
    }

    /// <summary>Gets the canonical lower-case media type.</summary>
    public string MediaType { get; }

    /// <summary>Gets the preferred file extension, without its leading dot.</summary>
    public string FileExtension { get; }

    /// <summary>Matches a JPEG, PNG, WebP, or AVIF media type, ignoring leading and trailing whitespace and case.</summary>
    /// <param name="mediaType">The media type, without parameters.</param>
    /// <returns>The canonical type, or null when no supported format matches.</returns>
    public static RasterImageMediaType? Find(string? mediaType)
    {
        var candidate = mediaType.AsSpan().Trim();
        foreach (var format in Formats)
        {
            if (candidate.Equals(format.MediaType, StringComparison.OrdinalIgnoreCase))
            {
                return format;
            }
        }

        return null;
    }
}
