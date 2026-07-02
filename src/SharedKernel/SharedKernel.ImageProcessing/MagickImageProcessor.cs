using ImageMagick;

namespace SharedKernel.ImageProcessing;

/// <summary>
/// ImageMagick-backed image processor that strips metadata from generated variants.
/// </summary>
public static class MagickImageProcessor
{
    /// <summary>
    /// Processes an image into the requested output variants.
    /// </summary>
    /// <param name="request">The image processing request.</param>
    /// <param name="ct">A token that can cancel processing before each variant is created.</param>
    /// <returns>The decoded image metadata and generated variants.</returns>
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
            EnsureSupportedInputSignature(request.Content);
            EnsureLimitValues(request.Limits);
            ApplyResourceLimits();

            var imageInfo = ProbeImage(request.Content);
            EnsureWithinLimits(imageInfo.Width, imageInfo.Height, request.Limits);

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
        image.Quality = (uint)quality;

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

    private static int ValidateQuality(int Quality)
        => Quality is < 1 or > 100
            ? throw new ArgumentOutOfRangeException(nameof(Quality), Quality, "Image variant quality must be between 1 and 100.")
            : Quality;

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
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported image output format.")
    };

    private static void ApplyResourceLimits()
    {
        ResourceLimits.Width = (ulong)ImageProcessingLimits.WebDefault.MaxWidth;
        ResourceLimits.Height = (ulong)ImageProcessingLimits.WebDefault.MaxHeight;
        ResourceLimits.Area = (ulong)ImageProcessingLimits.WebDefault.MaxPixelCount;
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
        if (header.Length >= 2 && header[0] == 0xFF && header[1] == 0xD8)
        {
            return true;
        }

        if (header.Length >= 8
            && header[0] == 0x89
            && header[1] == 0x50
            && header[2] == 0x4E
            && header[3] == 0x47
            && header[4] == 0x0D
            && header[5] == 0x0A
            && header[6] == 0x1A
            && header[7] == 0x0A)
        {
            return true;
        }

        return header.Length >= 12
            && (header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8)
                || header[4..12].SequenceEqual("ftypavif"u8));
    }
}
