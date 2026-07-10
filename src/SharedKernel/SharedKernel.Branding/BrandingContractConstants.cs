namespace SharedKernel.Branding;

/// <summary>
/// Defines branding contract limits shared by API clients and stores.
/// </summary>
public static class BrandingContractConstants
{
    /// <summary>
    /// Maximum brand name length.
    /// </summary>
    public const int MaxBrandNameLength = 120;

    /// <summary>
    /// Maximum CSS color value length.
    /// </summary>
    public const int MaxCssColorLength = 7;

    /// <summary>
    /// Maximum font family value length.
    /// </summary>
    public const int MaxFontFamilyLength = 120;

    /// <summary>
    /// Maximum logo URI length.
    /// </summary>
    public const int MaxLogoUriLength = 2048;
}
