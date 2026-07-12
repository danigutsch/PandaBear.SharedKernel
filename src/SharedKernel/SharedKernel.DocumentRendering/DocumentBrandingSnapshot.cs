namespace SharedKernel.DocumentRendering;

/// <summary>
/// Captures branding values used to decorate one document revision.
/// </summary>
public sealed record DocumentBrandingSnapshot(
    string Version,
    string BrandName,
    Uri? LogoUri,
    string PrimaryColor,
    string AccentColor,
    string BackgroundColor,
    string TextColor,
    string HeadingFontFamily,
    string BodyFontFamily,
    string FooterText)
{
    /// <summary>
    /// Creates a branding snapshot with default visual tokens.
    /// </summary>
    public DocumentBrandingSnapshot(string Version, string BrandName, Uri? LogoUri)
        : this(
            Version,
            BrandName,
            LogoUri,
            "#000000",
            "#000000",
            "#ffffff",
            "#000000",
            "system-ui, sans-serif",
            "system-ui, sans-serif",
            BrandName)
    {
    }

    /// <summary>
    /// Deconstructs the stable core branding identity.
    /// </summary>
    public void Deconstruct(out string Version, out string BrandName, out Uri? LogoUri)
    {
        Version = this.Version;
        BrandName = this.BrandName;
        LogoUri = this.LogoUri;
    }
}
