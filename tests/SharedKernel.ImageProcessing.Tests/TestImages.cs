using ImageMagick;

namespace SharedKernel.ImageProcessing.Tests;

internal static class TestImages
{
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

    public static MemoryStream CreateOrientedJpeg(uint width, uint height)
    {
        using var image = new MagickImage(MagickColors.Blue, width, height);
        image.Format = MagickFormat.Jpeg;
        image.Orientation = OrientationType.RightTop;
        image.SetProfile(CreateOrientationProfile());

        var stream = new MemoryStream();
        image.Write(stream);
        stream.Position = 0;

        return stream;
    }

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

    public static MemoryStream CreateImage(uint width, uint height, MagickFormat format)
    {
        using var image = new MagickImage(MagickColors.Blue, width, height);
        image.Format = format;

        var stream = new MemoryStream();
        image.Write(stream);
        stream.Position = 0;

        return stream;
    }

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

    public static MagickFormat ReadFormat(ReadOnlyMemory<byte> content)
    {
        using var image = new MagickImage(content.ToArray());

        return image.Format;
    }

    public static bool HasProfile(ReadOnlyMemory<byte> content)
    {
        using var image = new MagickImage(content.ToArray());

        return image.ProfileNames.Any();
    }

    public static ColorSpace ReadColorSpace(ReadOnlyMemory<byte> content)
    {
        using var image = new MagickImage(content.ToArray());

        return image.ColorSpace;
    }

    public static bool HasIcoHeader(ReadOnlyMemory<byte> content)
    {
        var span = content.Span;

        return span.Length >= 4 && span[0] == 0x00 && span[1] == 0x00 && span[2] == 0x01 && span[3] == 0x00;
    }
}
