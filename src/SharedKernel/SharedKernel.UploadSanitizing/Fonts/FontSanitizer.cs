using SharedKernel.Results;

namespace SharedKernel.UploadSanitizing.Fonts;

/// <summary>
/// Accepts an upload only when its own bytes are a single, structurally sound WOFF2 font (W3C WOFF File Format 2.0): the <c>wOF2</c>
/// signature, a TrueType or CFF flavour (font collections are refused), a declared length equal to the file's, a zero reserved field, a
/// table directory whose UIntBase128 numbers and transform versions are well formed, compressed data that fits the file, and metadata and
/// private blocks on four-byte boundaries inside the file after what precedes them. The bytes are returned unchanged. The compressed font
/// data itself is not decoded; a browser does that and drops a font it cannot use.
/// </summary>
/// <remarks>
/// Untrusted input never throws: every rejection is an <see cref="ResultStatus.Invalid"/> result with one of the message constants on
/// <see cref="FontSanitizerOptions.Field"/>. Declared lengths and offsets are compared with the bytes present in 64-bit arithmetic, so they
/// never overflow and no allocation depends on them. Work is linear in the input size.
/// </remarks>
public static class FontSanitizer
{
    /// <summary>The message of a file that does not start like a WOFF2 font.</summary>
    public const string NotAFontMessage = "The file must be a font in an accepted format.";

    /// <summary>The message of a font whose structure is damaged or not supported, such as a font collection.</summary>
    public const string MalformedMessage = "The font file is damaged or uses a structure that is not accepted.";

    /// <summary>The message of a file larger than <see cref="FontSanitizerOptions.MaxBytes"/>.</summary>
    public const string TooLargeMessage = "The font file is larger than the accepted size.";

    /// <summary>Checks a font held in memory.</summary>
    /// <param name="data">The uploaded bytes; they are not changed.</param>
    /// <param name="options">The limit and the error field.</param>
    /// <returns>The font in one new array the size of <paramref name="data"/>, or a validation error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public static Result<SanitizedFont> Sanitize(ReadOnlySpan<byte> data, FontSanitizerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return Check(data, options) is { } message ? Invalid(options, message) : Result.Ok(new SanitizedFont(FontFormat.Woff2, data.ToArray()));
    }

    /// <summary>
    /// Checks a font in the caller's buffer without allocating a copy: the result's <see cref="SanitizedFont.Bytes"/> is
    /// <paramref name="buffer"/> itself. The buffer is never changed.
    /// </summary>
    /// <param name="buffer">The uploaded bytes.</param>
    /// <param name="options">The limit and the error field.</param>
    /// <returns>The font over <paramref name="buffer"/>, or a validation error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public static Result<SanitizedFont> SanitizeInPlace(Memory<byte> buffer, FontSanitizerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return Check(buffer.Span, options) is { } message ? Invalid(options, message) : Result.Ok(new SanitizedFont(FontFormat.Woff2, buffer));
    }

    /// <summary>
    /// Reads an upload from its current position to its end, at most <see cref="FontSanitizerOptions.MaxBytes"/> plus one byte, then checks
    /// it. The stream is not disposed. A seekable stream's length only sizes the first buffer, within the limit and at most 1 MiB.
    /// </summary>
    /// <param name="content">The upload.</param>
    /// <param name="options">The limit and the error field.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The font, or a validation error; a stream longer than the limit is <see cref="TooLargeMessage"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled.</exception>
    public static async Task<Result<SanitizedFont>> Sanitize(Stream content, FontSanitizerOptions options, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(options);

        var buffer = await UploadReader.Read(content, options.MaxBytes, ct).ConfigureAwait(false);
        return buffer is { } read ? SanitizeInPlace(read, options) : Invalid(options, TooLargeMessage);
    }

    private static string? Check(ReadOnlySpan<byte> data, FontSanitizerOptions options)
    {
        if (data.Length > options.MaxBytes)
        {
            return TooLargeMessage;
        }

        if (!Woff2Reader.HasSignature(data))
        {
            return NotAFontMessage;
        }

        return Woff2Reader.IsSound(data) ? null : MalformedMessage;
    }

    private static Result<SanitizedFont> Invalid(FontSanitizerOptions options, string message) =>
        Result.Invalid<SanitizedFont>(message, options.Field, message);
}
