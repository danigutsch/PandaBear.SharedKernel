using PandaBear.Results;

namespace PandaBear.ImageSanitizing;

/// <summary>
/// Accepts an upload only when its own bytes are a well-formed JPEG, PNG or WebP, and removes the metadata that can reveal where and with
/// what a photo was taken: Exif (with GPS), XMP, Photoshop/IPTC data, comments, text and time chunks, and bytes after the end of the image.
/// Nothing is re-encoded and no native library is used: the pixel data and colour profiles are kept byte for byte. Anything that does not
/// parse exactly is rejected rather than guessed at.
/// </summary>
/// <remarks>
/// Untrusted input never throws: every rejection is an <see cref="ResultStatus.Invalid"/> result with one of the message constants on
/// <see cref="ImageSanitizerOptions.Field"/>. Declared lengths inside the file are checked against the bytes present before use, so no
/// allocation depends on them. Work is linear in the input size.
/// </remarks>
public static class ImageSanitizer
{
    /// <summary>The message of a file that is not a JPEG, PNG or WebP.</summary>
    public const string NotAnImageMessage = "The file must be a JPEG, PNG or WebP image.";

    /// <summary>The message of an image whose structure is damaged or not supported.</summary>
    public const string MalformedMessage = "The image file is damaged or uses a structure that is not accepted.";

    /// <summary>The message of a file larger than <see cref="ImageSanitizerOptions.MaxBytes"/>.</summary>
    public const string TooLargeMessage = "The image file is larger than the accepted size.";

    /// <summary>The message of an image wider or taller than <see cref="ImageSanitizerOptions.MaxDimension"/>.</summary>
    public const string DimensionsTooLargeMessage = "The image is wider or taller than the accepted size.";

    private const int StreamChunk = 81_920;

    private const int MaxInitialCapacity = 1024 * 1024;

    /// <summary>Recognizes the format, removes the metadata and reads the size of an image held in memory.</summary>
    /// <param name="data">The uploaded bytes; they are not changed.</param>
    /// <param name="options">The limits and the error field.</param>
    /// <returns>The sanitized image in one new array the size of <paramref name="data"/>, or a validation error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public static Result<SanitizedImage> Sanitize(ReadOnlySpan<byte> data, ImageSanitizerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (data.Length > options.MaxBytes)
        {
            return Invalid(options, TooLargeMessage);
        }

        return Detect(data) is { } format ? Clean(data.ToArray(), format, options) : Invalid(options, NotAnImageMessage);
    }

    /// <summary>
    /// Sanitizes an image in the caller's buffer without allocating a copy: the kept bytes are moved towards the start of
    /// <paramref name="buffer"/>, and the result's <see cref="SanitizedImage.Bytes"/> is a slice of it.
    /// </summary>
    /// <param name="buffer">The uploaded bytes. Its contents are unspecified after a rejection; do not use it then.</param>
    /// <param name="options">The limits and the error field.</param>
    /// <returns>The sanitized image over <paramref name="buffer"/>, or a validation error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public static Result<SanitizedImage> SanitizeInPlace(Memory<byte> buffer, ImageSanitizerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (buffer.Length > options.MaxBytes)
        {
            return Invalid(options, TooLargeMessage);
        }

        return Detect(buffer.Span) is { } format ? Clean(buffer, format, options) : Invalid(options, NotAnImageMessage);
    }

    /// <summary>
    /// Reads an upload from its current position to its end, at most <see cref="ImageSanitizerOptions.MaxBytes"/> plus one byte, then
    /// sanitizes it in place. The stream is not disposed. A seekable stream's length only sizes the first buffer, within the limit.
    /// </summary>
    /// <param name="content">The upload.</param>
    /// <param name="options">The limits and the error field.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The sanitized image, or a validation error; a stream longer than the limit is <see cref="TooLargeMessage"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled.</exception>
    public static async Task<Result<SanitizedImage>> Sanitize(Stream content, ImageSanitizerOptions options, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(options);

        var limit = (int)options.MaxBytes + 1;
        var buffer = new byte[InitialCapacity(content, limit)];
        var total = 0;
        while (true)
        {
            if (total == buffer.Length)
            {
                if (total == limit)
                {
                    return Invalid(options, TooLargeMessage);
                }

                Array.Resize(ref buffer, (int)Math.Min((long)buffer.Length * 2, limit));
            }

            var read = await content.ReadAsync(buffer.AsMemory(total), ct).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return SanitizeInPlace(buffer.AsMemory(0, total), options);
    }

    private static int InitialCapacity(Stream content, int limit)
    {
        long hint = StreamChunk;
        if (content.CanSeek)
        {
            // One more than the remaining length, so the read that returns zero needs no larger buffer. The length is only a hint: a
            // stream may report more than it holds, so it never sizes the buffer beyond MaxInitialCapacity; reads grow it from there.
            hint = Math.Max(content.Length - content.Position, 0) + 1;
        }

        return (int)Math.Clamp(hint, 1, Math.Min(limit, MaxInitialCapacity));
    }

    private static ImageFormat? Detect(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return ImageFormat.Jpeg;
        }

        if (data.StartsWith(PngSanitizer.Signature))
        {
            return ImageFormat.Png;
        }

        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
        {
            return ImageFormat.Webp;
        }

        return null;
    }

    private static Result<SanitizedImage> Clean(Memory<byte> buffer, ImageFormat format, ImageSanitizerOptions options)
    {
        var span = buffer.Span;
        int width;
        int height;
        var length = format switch
        {
            ImageFormat.Jpeg => JpegSanitizer.Clean(span, out width, out height),
            ImageFormat.Png => PngSanitizer.Clean(span, out width, out height),
            _ => WebpSanitizer.Clean(span, out width, out height),
        };

        if (length < 0 || width < 1 || height < 1)
        {
            return Invalid(options, MalformedMessage);
        }

        if (width > options.MaxDimension || height > options.MaxDimension)
        {
            return Invalid(options, DimensionsTooLargeMessage);
        }

        return Result.Ok(new SanitizedImage(format, buffer[..length], width, height));
    }

    private static Result<SanitizedImage> Invalid(ImageSanitizerOptions options, string message) =>
        Result.Invalid<SanitizedImage>(message, options.Field, message);
}
