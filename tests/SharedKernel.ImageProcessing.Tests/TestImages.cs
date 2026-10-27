using ImageMagick;

namespace PandaBear.ImageProcessing.Tests;

/// <summary>
/// Builds in-memory test images and inspects processed image bytes for the image processing tests.
/// </summary>
internal static class TestImages
{
    /// <summary>
    /// Creates a blue JPEG of the given size that carries a custom profile named "test-profile", positioned at the start.
    /// </summary>
    public static MemoryStream CreateJpegWithProfile(uint width, uint height)
    {
        using var image = new MagickImage(MagickColors.Blue, width, height);
        image.Format = MagickFormat.Jpeg;
        image.SetProfile(new ImageProfile("test-profile", [0x01, 0x02, 0x03]));

        var stream = new MemoryStream();
        image.Write(stream);
        stream.Position = 0;

        return stream;
    }

    /// <summary>
    /// Creates a blue JPEG of the given size with an EXIF profile that sets orientation 6, positioned at the start.
    /// </summary>
    public static MemoryStream CreateOrientedJpeg(uint width, uint height)
    {
        using var image = new MagickImage(MagickColors.Blue, width, height);
        image.Format = MagickFormat.Jpeg;
        image.Orientation = Orientation.RightTop;
        image.SetProfile(CreateOrientationProfile());

        var stream = new MemoryStream();
        image.Write(stream);
        stream.Position = 0;

        return stream;
    }

    /// <summary>
    /// Creates a JPEG of the given size whose color space is converted to CMYK using the US Web Coated SWOP profile, positioned at the start.
    /// </summary>
    public static MemoryStream CreateCmykJpeg(uint width, uint height)
    {
        using var image = new MagickImage(MagickColors.Cyan, width, height);
        image.Format = MagickFormat.Jpeg;
        image.TransformColorSpace(ColorProfiles.SRGB, ColorProfiles.USWebCoatedSWOP);

        var stream = new MemoryStream();
        image.Write(stream);
        stream.Position = 0;

        return stream;
    }

    /// <summary>
    /// Creates a blue image of the given size encoded in the given format, positioned at the start.
    /// </summary>
    public static MemoryStream CreateImage(uint width, uint height, MagickFormat format)
    {
        using var image = new MagickImage(MagickColors.Blue, width, height);
        image.Format = format;

        var stream = new MemoryStream();
        image.Write(stream);
        stream.Position = 0;

        return stream;
    }

    /// <summary>
    /// Creates a 64x32 AVIF image whose major brand bytes (offsets 8 to 11) are replaced by the four-character brand, and throws ArgumentException when the brand is not four characters long.
    /// </summary>
    public static MemoryStream CreateAvifWithMajorBrand(string majorBrand)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(majorBrand);

        if (majorBrand.Length != 4)
        {
            throw new ArgumentException("AVIF major brand must be four bytes.", nameof(majorBrand));
        }

        var stream = CreateImage(64, 32, MagickFormat.Avif);
        var buffer = stream.GetBuffer();
        buffer[8] = (byte)majorBrand[0];
        buffer[9] = (byte)majorBrand[1];
        buffer[10] = (byte)majorBrand[2];
        buffer[11] = (byte)majorBrand[3];
        stream.Position = 0;

        return stream;
    }

    private static ExifProfile CreateOrientationProfile()
    {
        var profile = new ExifProfile();
        profile.SetValue(ExifTag.Orientation, (ushort)6);

        return profile;
    }

    /// <summary>
    /// Decodes the bytes and returns the detected image format.
    /// </summary>
    public static MagickFormat ReadFormat(ReadOnlyMemory<byte> content)
    {
        using var image = new MagickImage(content.ToArray());

        return image.Format;
    }

    /// <summary>
    /// Decodes the bytes and returns whether the image carries any profile.
    /// </summary>
    public static bool HasProfile(ReadOnlyMemory<byte> content)
    {
        using var image = new MagickImage(content.ToArray());

        return image.ProfileNames.Any();
    }

    /// <summary>
    /// Decodes the bytes and returns the image color space.
    /// </summary>
    public static ColorSpace ReadColorSpace(ReadOnlyMemory<byte> content)
    {
        using var image = new MagickImage(content.ToArray());

        return image.ColorSpace;
    }

    /// <summary>
    /// Returns whether the bytes start with the ICO header 00 00 01 00.
    /// </summary>
    public static bool HasIcoHeader(ReadOnlyMemory<byte> content)
    {
        var span = content.Span;

        return span.Length >= 4 && span[0] == 0x00 && span[1] == 0x00 && span[2] == 0x01 && span[3] == 0x00;
    }
}
