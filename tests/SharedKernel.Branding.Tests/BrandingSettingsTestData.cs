namespace PandaBear.Branding.Tests;

/// <summary>
/// Shared valid branding input for the branding settings tests.
/// </summary>
internal static class BrandingSettingsTestData
{
    /// <summary>
    /// Font families accepted by validation in the tests: Inter and Source Serif 4.
    /// </summary>
    public static readonly string[] AllowedFontFamilies = ["Inter", "Source Serif 4"];

    /// <summary>
    /// Creates a valid request whose font names and accent color use non-canonical casing so tests can check normalization.
    /// </summary>
    public static BrandingSettingsDto ValidRequest() => new()
    {
        BrandName = "Example Brand",
        PrimaryColor = "#112233",
        AccentColor = "#aabbcc",
        BackgroundColor = "#FFFFFF",
        TextColor = "#000000",
        HeadingFontFamily = "inter",
        BodyFontFamily = "SOURCE SERIF 4",
        LogoUri = "/assets/logo.svg",
    };
}
