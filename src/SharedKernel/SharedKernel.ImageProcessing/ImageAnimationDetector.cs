using System.Buffers.Binary;

namespace SharedKernel.ImageProcessing;

/// <summary>
/// Detects animated PNG and WebP images from their container headers, before any pixel data is decoded.
/// </summary>
/// <remarks>
/// The walk reads only chunk headers and the WebP extended-format flags. A PNG is animated when an <c>acTL</c> chunk
/// precedes the first <c>IDAT</c> chunk. A WebP is animated when its <c>VP8X</c> header sets the animation flag or the
/// container holds an <c>ANIM</c> or <c>ANMF</c> chunk. Offsets use 64-bit arithmetic and are compared to the remaining
/// length before each read, the walk stops after <see cref="MaxChunkCount" /> chunks, and any inconsistency yields
/// <see cref="ImageAnimationStatus.Malformed" />. Chunk checksums are not verified.
/// </remarks>
public static class ImageAnimationDetector
{
    /// <summary>
    /// The maximum number of chunks the walk inspects before it reports <see cref="ImageAnimationStatus.Malformed" />.
    /// </summary>
    public const int MaxChunkCount = 4096;

    private const int PngSignatureLength = 8;
    private const int PngChunkHeaderLength = 8;
    private const int PngChunkCrcLength = 4;
    private const int RiffHeaderLength = 12;
    private const int RiffChunkHeaderLength = 8;
    private const int Vp8xMinimumPayloadLength = 10;
    private const byte Vp8xAnimationFlag = 0x02;

    /// <summary>
    /// Detects whether in-memory PNG or WebP content declares an animation.
    /// </summary>
    /// <param name="content">The complete container bytes.</param>
    /// <returns>The detection outcome.</returns>
    public static ImageAnimationStatus Detect(ReadOnlySpan<byte> content)
        => Detect(new SpanAnimationByteSource(content));

    /// <summary>
    /// Detects whether PNG or WebP content in a seekable stream declares an animation. The walk starts at the current
    /// position, and the position is restored before the method returns.
    /// </summary>
    /// <param name="content">The seekable container stream.</param>
    /// <returns>The detection outcome.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="content" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="content" /> cannot be read or cannot seek.</exception>
    public static ImageAnimationStatus Detect(Stream content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!content.CanRead || !content.CanSeek)
        {
            throw new ArgumentException("Animation detection requires a readable, seekable stream.", nameof(content));
        }

        var origin = content.Position;
        try
        {
            return Detect(new StreamAnimationByteSource(content, origin));
        }
        finally
        {
            content.Position = origin;
        }
    }

    private static ImageAnimationStatus Detect<TSource>(TSource source)
        where TSource : IAnimationByteSource, allows ref struct
    {
        Span<byte> header = stackalloc byte[RiffHeaderLength];
        if (source.TryRead(0, header[..PngSignatureLength]) && header[..PngSignatureLength].SequenceEqual(PngSignature))
        {
            return DetectPng(source);
        }

        if (source.TryRead(0, header) && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
        {
            return DetectWebP(source, BinaryPrimitives.ReadUInt32LittleEndian(header[4..8]));
        }

        return ImageAnimationStatus.Unsupported;
    }

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static ImageAnimationStatus DetectPng<TSource>(TSource source)
        where TSource : IAnimationByteSource, allows ref struct
    {
        Span<byte> chunkHeader = stackalloc byte[PngChunkHeaderLength];
        long position = PngSignatureLength;

        for (var chunkIndex = 0; chunkIndex < MaxChunkCount; chunkIndex++)
        {
            if (!source.TryRead(position, chunkHeader))
            {
                return ImageAnimationStatus.Malformed;
            }

            var dataLength = BinaryPrimitives.ReadUInt32BigEndian(chunkHeader[..4]);
            if (dataLength > int.MaxValue)
            {
                return ImageAnimationStatus.Malformed;
            }

            var next = position + PngChunkHeaderLength + dataLength + PngChunkCrcLength;
            if (next > source.Length)
            {
                return ImageAnimationStatus.Malformed;
            }

            var type = chunkHeader[4..8];
            if (chunkIndex == 0 && !type.SequenceEqual("IHDR"u8))
            {
                return ImageAnimationStatus.Malformed;
            }

            if (type.SequenceEqual("acTL"u8))
            {
                return ImageAnimationStatus.Animated;
            }

            if (type.SequenceEqual("IDAT"u8))
            {
                return ImageAnimationStatus.Static;
            }

            if (type.SequenceEqual("IEND"u8))
            {
                return ImageAnimationStatus.Malformed;
            }

            position = next;
        }

        return ImageAnimationStatus.Malformed;
    }

    private static ImageAnimationStatus DetectWebP<TSource>(TSource source, uint riffSize)
        where TSource : IAnimationByteSource, allows ref struct
    {
        var riffEnd = 8L + riffSize;
        if (riffSize < 4 || riffEnd > source.Length)
        {
            return ImageAnimationStatus.Malformed;
        }

        Span<byte> chunkHeader = stackalloc byte[RiffChunkHeaderLength];
        Span<byte> flags = stackalloc byte[1];
        long position = RiffHeaderLength;

        for (var chunkIndex = 0; chunkIndex < MaxChunkCount; chunkIndex++)
        {
            if (position >= riffEnd)
            {
                return chunkIndex == 0 ? ImageAnimationStatus.Malformed : ImageAnimationStatus.Static;
            }

            if (riffEnd - position < RiffChunkHeaderLength || !source.TryRead(position, chunkHeader))
            {
                return ImageAnimationStatus.Malformed;
            }

            var payloadLength = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader[4..8]);
            var payloadEnd = position + RiffChunkHeaderLength + payloadLength;
            if (payloadEnd > riffEnd)
            {
                return ImageAnimationStatus.Malformed;
            }

            var fourCc = chunkHeader[..4];
            if (chunkIndex == 0)
            {
                var status = InspectFirstWebPChunk(source, fourCc, position, payloadLength, flags);
                if (status is not null)
                {
                    return status.Value;
                }
            }

            if (fourCc.SequenceEqual("ANIM"u8) || fourCc.SequenceEqual("ANMF"u8))
            {
                return ImageAnimationStatus.Animated;
            }

            position = payloadEnd + (payloadLength & 1);
        }

        return ImageAnimationStatus.Malformed;
    }

    private static ImageAnimationStatus? InspectFirstWebPChunk<TSource>(
        TSource source,
        ReadOnlySpan<byte> fourCc,
        long position,
        uint payloadLength,
        Span<byte> flags)
        where TSource : IAnimationByteSource, allows ref struct
    {
        if (fourCc.SequenceEqual("VP8 "u8) || fourCc.SequenceEqual("VP8L"u8))
        {
            return null;
        }

        if (!fourCc.SequenceEqual("VP8X"u8)
            || payloadLength < Vp8xMinimumPayloadLength
            || !source.TryRead(position + RiffChunkHeaderLength, flags))
        {
            return ImageAnimationStatus.Malformed;
        }

        return (flags[0] & Vp8xAnimationFlag) != 0 ? ImageAnimationStatus.Animated : null;
    }
}
