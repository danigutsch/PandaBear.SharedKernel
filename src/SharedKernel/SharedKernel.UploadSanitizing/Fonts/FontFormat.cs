namespace SharedKernel.UploadSanitizing.Fonts;

/// <summary>The font formats <see cref="FontSanitizer"/> accepts, recognized by their leading bytes, never by file name or declared content type.</summary>
public enum FontFormat
{
    /// <summary>WOFF2 (W3C WOFF File Format 2.0, signature <c>wOF2</c>), a single font; font collections are refused.</summary>
    Woff2 = 0,
}
