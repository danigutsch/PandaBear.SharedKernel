using System.Diagnostics.CodeAnalysis;

namespace SharedKernel.Branding;

/// <summary>
/// Carries branding settings across API boundaries.
/// </summary>
public sealed class BrandingSettingsDto
{
    /// <summary>
    /// Gets or sets the display brand name.
    /// </summary>
    public string BrandName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the primary CSS color.
    /// </summary>
    public string PrimaryColor { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the accent CSS color.
    /// </summary>
    public string AccentColor { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the background CSS color.
    /// </summary>
    public string BackgroundColor { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the text CSS color.
    /// </summary>
    public string TextColor { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the heading font family.
    /// </summary>
    public string HeadingFontFamily { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the body font family.
    /// </summary>
    public string BodyFontFamily { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional logo URI.
    /// </summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "The contract accepts root-relative paths and absolute HTTPS URIs.")]
    public string? LogoUri { get; set; }
}
