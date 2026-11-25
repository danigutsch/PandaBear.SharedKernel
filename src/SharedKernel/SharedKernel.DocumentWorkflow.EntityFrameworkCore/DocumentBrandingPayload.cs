using SharedKernel.DocumentRendering;

namespace SharedKernel.DocumentWorkflow.EntityFrameworkCore;

/// <summary>Preserves the branding properties stored in version-one document payloads.</summary>
/// <param name="Version">The branding revision.</param>
/// <param name="BrandName">The displayed name.</param>
/// <param name="LogoUri">The captured logo URI.</param>
/// <param name="PrimaryColor">The primary color.</param>
/// <param name="AccentColor">The accent color.</param>
/// <param name="BackgroundColor">The background color.</param>
/// <param name="TextColor">The text color.</param>
/// <param name="HeadingFontFamily">The heading font.</param>
/// <param name="BodyFontFamily">The body font.</param>
/// <param name="FooterText">The captured footer text.</param>
internal sealed record DocumentBrandingPayload(
    string Version, string BrandName, Uri? LogoUri, string PrimaryColor, string AccentColor,
    string BackgroundColor, string TextColor, string HeadingFontFamily, string BodyFontFamily, string FooterText)
{
    /// <summary>Converts a renderer branding snapshot to the versioned storage shape.</summary>
    public static DocumentBrandingPayload? From(DocumentBrandingSnapshot? branding) => branding is null
        ? null
        : new(branding.Version, branding.BrandName, branding.LogoUri, branding.PrimaryColor, branding.AccentColor,
            branding.BackgroundColor, branding.TextColor, branding.HeadingFontFamily, branding.BodyFontFamily, branding.FooterText);

    /// <summary>Reconstructs the captured renderer branding.</summary>
    public DocumentBrandingSnapshot ToBranding() =>
        new(Version, BrandName, LogoUri, PrimaryColor, AccentColor, BackgroundColor, TextColor, HeadingFontFamily, BodyFontFamily, FooterText);
}
