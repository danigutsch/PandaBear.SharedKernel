namespace PandaBear.ImageSanitizing;

/// <summary>The image formats <see cref="ImageSanitizer"/> accepts, recognized by their leading bytes, never by file name or declared content type.</summary>
public enum ImageFormat
{
    /// <summary>JPEG (<c>FF D8 FF</c>).</summary>
    Jpeg = 0,

    /// <summary>PNG (the eight-byte PNG signature).</summary>
    Png = 1,

    /// <summary>WebP (a <c>RIFF</c> container of type <c>WEBP</c>).</summary>
    Webp = 2,

    /// <summary>
    /// AVIF (an ISOBMFF file whose <c>ftyp</c> box lists the <c>avif</c> or <c>avis</c> brand). Accepted only with
    /// <see cref="ImageSanitizerOptions.AllowAvif"/>, and kept with its metadata.
    /// </summary>
    Avif = 3,
}
