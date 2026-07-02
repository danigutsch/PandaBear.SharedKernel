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
    public static ImageProcessingResult Process(ImageProcessingRequest request, CancellationToken ct = default)
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
        if (limits.MaxWidth <= 0 || limits.MaxHeight <= 0 || limits.MaxPixelCount <= 0)
        {
            throw new ArgumentException("Image processing limits must be greater than zero.", nameof(limits));
        }

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

        if (request.MaxWidth <= 0 || request.MaxHeight <= 0)
        {
            throw new ArgumentException("Image variant maximum dimensions must be greater than zero.", nameof(request));
        }

        if (request.Quality is < 1 or > 100)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(request.Quality, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(request.Quality, 100);
        }

        using var image = source.Clone();
        image.Strip();
        image.ColorSpace = ColorSpace.sRGB;
        var maxHeight = request.MaxHeight is null ? 0 : (uint)request.MaxHeight.Value;
        image.Resize(new MagickGeometry((uint)request.MaxWidth, maxHeight) { Greater = true });
        if (request.Format == ImageOutputFormat.Ico)
        {
            PadToSquareIcon((MagickImage)image, (uint)request.MaxWidth, maxHeight);
        }

        image.Format = ToMagickFormat(request.Format);
        image.Quality = (uint)request.Quality;

        using var output = new MemoryStream();
        image.Write(output);

        return new ProcessedImageVariant(
            request.Name,
            request.Format,
            (int)image.Width,
            (int)image.Height,
            output.ToArray().AsMemory());
    }

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
}
