using System.Buffers.Binary;
using System.Text;
using ImageMagick;

namespace PandaBear.ImageProcessing.Tests;

/// <summary>
/// Builds RGBA PNG sources and inspects PNG output pixels and chunks for the PNG output tests.
/// </summary>
internal static class PngTestImages
{
    private const int SignatureLength = 8;
    private const int ChunkHeaderLength = 8;
    private const int ChunkCrcLength = 4;

    /// <summary>
    /// Width of the gradient returned by <see cref="CreateGradientPixels"/>; every alpha value from 0 through 255 appears once per row.
    /// </summary>
    public const int GradientWidth = 256;

    /// <summary>
    /// Height of the gradient returned by <see cref="CreateGradientPixels"/>.
    /// </summary>
    public const int GradientHeight = 4;

    /// <summary>
    /// Returns RGBA bytes for a gradient whose alpha equals the column index (fully transparent through opaque) and whose colors vary per pixel.
    /// </summary>
    public static byte[] CreateGradientPixels()
    {
        var pixels = new byte[GradientWidth * GradientHeight * 4];
        for (var y = 0; y < GradientHeight; y++)
        {
            for (var x = 0; x < GradientWidth; x++)
            {
                var index = ((y * GradientWidth) + x) * 4;
                pixels[index] = (byte)x;
                pixels[index + 1] = (byte)(255 - x);
                pixels[index + 2] = (byte)(y * 60);
                pixels[index + 3] = (byte)x;
            }
        }

        return pixels;
    }

    /// <summary>
    /// Encodes 8-bit RGBA pixel bytes of the given size as a PNG stream positioned at the start.
    /// </summary>
    public static MemoryStream CreatePng(byte[] rgbaPixels, uint width, uint height)
    {
        using var image = new MagickImage();
        image.ReadPixels(rgbaPixels, new PixelReadSettings(width, height, StorageType.Char, PixelMapping.RGBA));
        image.Format = MagickFormat.Png;

        var stream = new MemoryStream();
        image.Write(stream);
        stream.Position = 0;

        return stream;
    }

    /// <summary>
    /// Decodes the bytes and returns their pixels as 8-bit RGBA bytes.
    /// </summary>
    public static byte[] ReadRgbaPixels(ReadOnlyMemory<byte> content)
    {
        using var image = new MagickImage(content.ToArray());
        using var pixels = image.GetPixels();

        return pixels.ToByteArray(PixelMapping.RGBA) ?? [];
    }

    /// <summary>
    /// Returns the bit depth and color type stored in the IHDR chunk, which must directly follow the PNG signature.
    /// </summary>
    public static (byte BitDepth, byte ColorType) ReadHeader(ReadOnlyMemory<byte> content)
    {
        var span = content.Span;
        if (span.Length < SignatureLength + ChunkHeaderLength + 10
            || !span.Slice(SignatureLength + 4, 4).SequenceEqual("IHDR"u8))
        {
            throw new InvalidDataException("PNG bytes do not start with an IHDR chunk.");
        }

        var data = span[(SignatureLength + ChunkHeaderLength)..];

        return (data[8], data[9]);
    }

    /// <summary>
    /// Walks the chunks after the PNG signature and returns their four-character types in order.
    /// </summary>
    public static IReadOnlyList<string> ReadChunkTypes(ReadOnlyMemory<byte> content)
    {
        var span = content.Span;
        var chunkTypes = new List<string>();
        long position = SignatureLength;
        while (position + ChunkHeaderLength <= span.Length)
        {
            var offset = (int)position;
            var length = BinaryPrimitives.ReadUInt32BigEndian(span[offset..]);
            chunkTypes.Add(Encoding.ASCII.GetString(span.Slice(offset + 4, 4)));
            position += ChunkHeaderLength + (long)length + ChunkCrcLength;
        }

        return chunkTypes;
    }
}
