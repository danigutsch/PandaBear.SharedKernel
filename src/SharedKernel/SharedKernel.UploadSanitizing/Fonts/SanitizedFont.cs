namespace PandaBear.UploadSanitizing.Fonts;

/// <summary>
/// A font whose structure <see cref="FontSanitizer"/> checked. Only the sanitizer creates instances, so holding one means the bytes passed
/// those checks. The bytes are the upload unchanged: a WOFF2 font carries no location or camera metadata to remove.
/// </summary>
public sealed class SanitizedFont
{
    /// <summary>Initializes a new instance of the <see cref="SanitizedFont"/> class.</summary>
    /// <param name="format">The format.</param>
    /// <param name="bytes">The checked bytes.</param>
    internal SanitizedFont(FontFormat format, ReadOnlyMemory<byte> bytes)
    {
        Format = format;
        Bytes = bytes;
    }

    /// <summary>Gets the format recognized from the bytes.</summary>
    public FontFormat Format { get; }

    /// <summary>Gets the font, byte for byte as uploaded.</summary>
    public ReadOnlyMemory<byte> Bytes { get; }

    /// <summary>Gets the usual file extension of the format, with its dot: <c>.woff2</c>.</summary>
    public string FileExtension => Format switch
    {
        _ => ".woff2",
    };

    /// <summary>Gets the media type of the format: <c>font/woff2</c>.</summary>
    public string ContentType => Format switch
    {
        _ => "font/woff2",
    };
}
