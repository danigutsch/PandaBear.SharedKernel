namespace PandaBear.UploadSanitizing.Fonts;

/// <summary>The limit of <see cref="FontSanitizer"/> and the field its validation errors belong to.</summary>
public sealed record FontSanitizerOptions
{
    /// <summary>The largest value of <see cref="MaxBytes"/>: one byte less than the longest byte array, so the extra byte that proves a stream too long fits.</summary>
    public const long MaxBytesLimit = UploadReader.MaxBytesLimit;

    /// <summary>Gets options with the defaults: 2 MiB, field <c>file</c>.</summary>
    public static FontSanitizerOptions Default { get; } = new();

    /// <summary>Gets the largest accepted file size in bytes; at most this many bytes plus one are read from a stream. Default 2 MiB.</summary>
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
    } = 2 * 1024 * 1024;

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
