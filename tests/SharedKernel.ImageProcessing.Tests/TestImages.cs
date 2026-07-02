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

    public static bool HasIcoHeader(ReadOnlyMemory<byte> content)
    {
        var span = content.Span;

        return span.Length >= 4 && span[0] == 0x00 && span[1] == 0x00 && span[2] == 0x01 && span[3] == 0x00;
    }
}
