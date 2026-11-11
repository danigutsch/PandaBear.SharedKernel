namespace PandaBear.ImageSanitizing;

/// <summary>The limits of <see cref="ImageSanitizer"/> and the field its validation errors belong to.</summary>
public sealed record ImageSanitizerOptions
{
    /// <summary>The largest value of <see cref="MaxBytes"/>: one byte less than the longest byte array, so the extra byte that proves a stream too long fits.</summary>
    public const long MaxBytesLimit = 0x7FFFFFC6;

    /// <summary>Gets options with the defaults: 10 MiB, 20,000 pixels per side, field <c>file</c>.</summary>
    public static ImageSanitizerOptions Default { get; } = new();

    /// <summary>Gets the largest accepted file size in bytes; at most this many bytes plus one are read from a stream. Default 10 MiB.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1 or greater than <see cref="MaxBytesLimit"/>.</exception>
    public long MaxBytes
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxBytesLimit);
            field = value;
        }
    } = 10 * 1024 * 1024;

    /// <summary>Gets the largest accepted width and height in pixels. Default 20,000.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public int MaxDimension
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            field = value;
        }
    } = 20_000;

    /// <summary>Gets the name of the field the validation errors are reported on. Default <c>file</c>.</summary>
    /// <exception cref="ArgumentException">The value is <see langword="null"/>, empty or white space.</exception>
    public string Field
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value;
        }
    } = "file";
}
