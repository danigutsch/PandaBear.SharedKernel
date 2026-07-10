namespace SharedKernel.Branding.Tests;

internal static class BrandingSettingsTestData
{
    public static readonly string[] AllowedFontFamilies = ["Inter", "Source Serif 4"];

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
