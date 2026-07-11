namespace SharedKernel.DocumentRendering;

/// <summary>
/// Captures branding values used to decorate one document revision.
/// </summary>
public sealed record DocumentBrandingSnapshot(
    string Version,
    string BrandName,
    Uri? LogoUri);
