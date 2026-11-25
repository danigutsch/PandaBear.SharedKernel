using ImageMagick;

namespace SharedKernel.ImageProcessing;

/// <summary>
/// ImageMagick-backed image processor that strips metadata from generated variants.
/// </summary>
public static class MagickImageProcessor
{
    private const uint DefaultPngCompressionQuality = 75;

    static MagickImageProcessor()
    {
        ApplyResourceLimits(ImageProcessingLimits.WebDefault);
    }

    /// <summary>
    /// Processes an image into the requested output variants.
    /// </summary>
    /// <param name="request">The image processing request.</param>
    /// <param name="ct">A token that can cancel processing before each variant is created.</param>
    /// <returns>The decoded image metadata and generated variants.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> or its content, variants, or limits is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="request"/> requests no variants.</exception>
    /// <exception cref="ImageProcessingException">The image data cannot be decoded or encoded.</exception>
    public static ImageProcessingResult Process(ImageProcessingRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Content);
        ArgumentNullException.ThrowIfNull(request.Variants);
        ArgumentNullException.ThrowIfNull(request.Limits);

        if (request.Variants.Count == 0)
        {
            throw new ArgumentException("At least one image variant must be requested.", nameof(request));
        }

        try
        {
            EnsureStreamCanBeProbed(request.Content);
            EnsureSupportedInput(request.Content, request.Limits);
            ProbeWithinLimits(request.Content, request.Limits);

            using var image = new MagickImage(request.Content);
            image.AutoOrient();
            EnsureWithinLimits(image.Width, image.Height, request.Limits);

            var variants = new List<ProcessedImageVariant>(request.Variants.Count);
            foreach (var variant in request.Variants)
            {
                ct.ThrowIfCancellationRequested();
                variants.Add(CreateVariant(image, variant));
            }

            return new ImageProcessingResult((int)image.Width, (int)image.Height, variants);
        }
        catch (MagickException exception)
        {
            throw new ImageProcessingException("Image data could not be decoded or encoded.", exception);
        }
    }

    /// <summary>
    /// Reads the format, stored dimensions, and animation flag of an image from its headers without decoding pixel data.
    /// The stream must be seekable and positioned at the beginning; its position is restored before the method returns.
    /// </summary>
    /// <param name="content">The seekable image stream.</param>
    /// <param name="limits">The decoded image limits the stored dimensions must satisfy.</param>
    /// <returns>The probed image details.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> or <paramref name="limits"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the stream cannot seek, is not at the beginning, or the limits are invalid.</exception>
    /// <exception cref="ImageProcessingException">The image format is unsupported, the container is malformed, or the image exceeds the limits.</exception>
    public static ImageProbeResult Probe(Stream content, ImageProcessingLimits limits)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(limits);
        EnsureStreamCanBeProbed(content);

        try
        {
            EnsureSupportedInput(content, limits);
            var imageInfo = ProbeWithinLimits(content, limits);
            var format = ToInputFormat(imageInfo.Format);
            var isAnimated = DetectAnimation(content, format);

            return new ImageProbeResult(format, (int)imageInfo.Width, (int)imageInfo.Height, isAnimated);
        }
        catch (MagickException exception)
        {
            throw new ImageProcessingException("Image data could not be decoded.", exception);
        }
        finally
        {
            content.Position = 0;
        }
    }

    private static void EnsureSupportedInput(Stream content, ImageProcessingLimits limits)
    {
        EnsureSupportedInputSignature(content);
        EnsureLimitValues(limits);
    }

    private static MagickImageInfo ProbeWithinLimits(Stream content, ImageProcessingLimits limits)
    {
        var imageInfo = ProbeImage(content);
        EnsureWithinLimits(imageInfo.Width, imageInfo.Height, limits);

        return imageInfo;
    }

    private static ImageInputFormat ToInputFormat(MagickFormat format) => format switch
    {
        MagickFormat.Jpeg or MagickFormat.Jpg => ImageInputFormat.Jpeg,
        MagickFormat.Png
            or MagickFormat.Png8
            or MagickFormat.Png24
            or MagickFormat.Png32
            or MagickFormat.Png48
            or MagickFormat.Png64
            or MagickFormat.APng => ImageInputFormat.Png,
        MagickFormat.WebP => ImageInputFormat.WebP,
        MagickFormat.Avif => ImageInputFormat.Avif,
        _ => throw new ImageProcessingException("Image data uses an unsupported image format.")
    };

    private static bool DetectAnimation(Stream content, ImageInputFormat format) => format switch
    {
        ImageInputFormat.Png or ImageInputFormat.WebP => ImageAnimationDetector.Detect(content) switch
        {
            ImageAnimationStatus.Animated => true,
            ImageAnimationStatus.Static => false,
            _ => throw new ImageProcessingException("Image container is malformed.")
        },
        ImageInputFormat.Avif => HasMultipleFrames(content),
        ImageInputFormat.Jpeg => false,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported image input format.")
    };

    private static bool HasMultipleFrames(Stream content)
    {
        // FrameCount bounds the ping to two frames; ResourceLimits.ListLength does not bound image sequences.
        var settings = new MagickReadSettings { FrameCount = 2 };
        var position = content.Position;
        try
        {
            using var frames = new MagickImageCollection();
            frames.Ping(content, settings);

            return frames.Count > 1;
        }
        finally
        {
            content.Position = position;
        }
    }

    private static void EnsureStreamCanBeProbed(Stream content)
    {
        if (!content.CanSeek)
        {
            throw new ArgumentException("Image content stream must be seekable so decoded dimensions can be probed before decoding.", nameof(content));
        }

        if (content.Position != 0)
        {
            throw new ArgumentException("Image content stream must be positioned at the beginning before processing.", nameof(content));
        }
    }

    private static MagickImageInfo ProbeImage(Stream content)
    {
        var position = content.Position;
        try
        {
            return new MagickImageInfo(content);
        }
        finally
        {
            content.Position = position;
        }
    }

    private static void EnsureWithinLimits(uint width, uint height, ImageProcessingLimits limits)
    {
        if (width > limits.MaxWidth || height > limits.MaxHeight)
        {
            throw new ImageProcessingException("Decoded image exceeds the configured processing limits.");
        }

        var pixelCount = (long)width * height;
        if (pixelCount > limits.MaxPixelCount)
        {
            throw new ImageProcessingException("Decoded image exceeds the configured processing limits.");
        }
    }

    private static ProcessedImageVariant CreateVariant(MagickImage source, ImageVariantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Image variant name must be provided.", nameof(request));
        }

        if (request.MaxWidth <= 0 || request.MaxHeight is <= 0)
        {
            throw new ArgumentException("Image variant maximum dimensions must be greater than zero.", nameof(request));
        }

        var quality = ValidateQuality(request.Quality);

        using var image = source.Clone();
        NormalizeColorSpace((MagickImage)image);
        image.Strip();
        var maxHeight = request.MaxHeight is null ? 0 : (uint)request.MaxHeight.Value;
        image.Resize(new MagickGeometry((uint)request.MaxWidth, maxHeight) { Greater = true });
        if (request.Format == ImageOutputFormat.Ico)
        {
            PadToSquareIcon((MagickImage)image, (uint)request.MaxWidth, maxHeight);
        }

        image.Format = ToMagickFormat(request.Format);
        if (request.Format == ImageOutputFormat.Png)
        {
            ConfigureLosslessPng((MagickImage)image);
        }
        else
        {
            image.Quality = (uint)quality;
        }

        using var output = new MemoryStream();
        image.Write(output);

        return new ProcessedImageVariant(
            request.Name,
            request.Format,
            (int)image.Width,
            (int)image.Height,
            output.ToArray().AsMemory());
    }

    private static void NormalizeColorSpace(MagickImage image)
    {
        if (image.ColorSpace == ColorSpace.sRGB)
        {
            return;
        }

        if (!image.TransformColorSpace(ColorProfiles.SRGB))
        {
            image.ColorSpace = ColorSpace.sRGB;
        }
    }

    private static void ConfigureLosslessPng(MagickImage image)
    {
        // ImageMagick reads PNG "quality" as zlib level and filter digits, not as fidelity, and a clone inherits the
        // decoded source quality. Pin ImageMagick's own PNG default (zlib level 7, adaptive filtering) instead.
        image.Quality = DefaultPngCompressionQuality;

        // Write alpha as 8-bit RGBA so semi-transparent pixels never go through a palette reduction.
        if (image.HasAlpha)
        {
            image.Format = MagickFormat.Png32;
        }

        // Keep timestamps out of the output: "date" drops the date:* tEXt chunks and "time" drops tIME. This adds to
        // the png:exclude-chunk list Strip() sets, so text, EXIF, and profile chunks stay excluded too.
        image.Settings.SetDefine(MagickFormat.Png, "exclude-chunks", "date,time");
    }

    private static int ValidateQuality(int quality)
        => quality is < 1 or > 100
            ? throw new ArgumentOutOfRangeException(nameof(quality), quality, "Image variant quality must be between 1 and 100.")
            : quality;

    private static void PadToSquareIcon(MagickImage image, uint maxWidth, uint maxHeight)
    {
        var iconSize = maxHeight == 0 ? maxWidth : Math.Min(maxWidth, maxHeight);
        image.BackgroundColor = MagickColors.Transparent;
        image.Extent(iconSize, iconSize, Gravity.Center);
    }

    private static MagickFormat ToMagickFormat(ImageOutputFormat format) => format switch
    {
        ImageOutputFormat.Avif => MagickFormat.Avif,
        ImageOutputFormat.WebP => MagickFormat.WebP,
        ImageOutputFormat.Jpeg => MagickFormat.Jpeg,
        ImageOutputFormat.Ico => MagickFormat.Ico,
        ImageOutputFormat.Png => MagickFormat.Png,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported image output format.")
    };

    private static void ApplyResourceLimits(ImageProcessingLimits limits)
    {
        ResourceLimits.Width = (ulong)limits.MaxWidth;
        ResourceLimits.Height = (ulong)limits.MaxHeight;
        ResourceLimits.Area = (ulong)limits.MaxPixelCount;
        ResourceLimits.Memory = 256UL * 1024UL * 1024UL;
        ResourceLimits.Disk = 512UL * 1024UL * 1024UL;
        ResourceLimits.Thread = 1;
        ResourceLimits.Time = 30;
    }

    private static void EnsureLimitValues(ImageProcessingLimits limits)
    {
        if (limits.MaxWidth <= 0 || limits.MaxHeight <= 0 || limits.MaxPixelCount <= 0)
        {
            throw new ArgumentException("Image processing limits must be greater than zero.", nameof(limits));
        }

        if (limits.MaxWidth > ImageProcessingLimits.WebDefault.MaxWidth
            || limits.MaxHeight > ImageProcessingLimits.WebDefault.MaxHeight
            || limits.MaxPixelCount > ImageProcessingLimits.WebDefault.MaxPixelCount)
        {
            throw new ArgumentException("Image processing limits must not exceed the web default maximums.", nameof(limits));
        }
    }

    private static void EnsureSupportedInputSignature(Stream content)
    {
        Span<byte> header = stackalloc byte[12];
        var bytesRead = content.Read(header);
        content.Position = 0;

        if (!HasSupportedInputSignature(header[..bytesRead]))
        {
            throw new ImageProcessingException("Image data uses an unsupported image format.");
        }
    }

    private static bool HasSupportedInputSignature(ReadOnlySpan<byte> header)
    {
        var isJpeg = header.Length >= 2 && header[0] == 0xFF && header[1] == 0xD8;
        var isPng = header.Length >= 8
            && header[0] == 0x89
            && header[1] == 0x50
            && header[2] == 0x4E
            && header[3] == 0x47
            && header[4] == 0x0D
            && header[5] == 0x0A
            && header[6] == 0x1A
            && header[7] == 0x0A;
        var isWebP = header.Length >= 12
            && header[..4].SequenceEqual("RIFF"u8)
            && header[8..12].SequenceEqual("WEBP"u8);
        var isAvif = header.Length >= 12
            && header[4..8].SequenceEqual("ftyp"u8)
            && (header[8..12].SequenceEqual("avif"u8) || header[8..12].SequenceEqual("avis"u8));

        return isJpeg || isPng || isWebP || isAvif;
    }
}
