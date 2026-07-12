using System.Diagnostics.CodeAnalysis;
using SharedKernel.InputNormalization;
using SharedKernel.Results;

namespace SharedKernel.Branding;

/// <summary>
/// Represents validated reusable branding settings.
/// </summary>
public sealed class BrandingSettings
{
    private const string ValidationErrorDetail = "Branding settings are invalid.";

    private BrandingSettings(
        string brandName,
        string primaryColor,
        string accentColor,
        string backgroundColor,
        string textColor,
        string headingFontFamily,
        string bodyFontFamily,
        string? logoUri)
    {
        BrandName = brandName;
        PrimaryColor = primaryColor;
        AccentColor = accentColor;
        BackgroundColor = backgroundColor;
        TextColor = textColor;
        HeadingFontFamily = headingFontFamily;
        BodyFontFamily = bodyFontFamily;
        LogoUri = logoUri;
    }

    /// <summary>
    /// Gets the display brand name.
    /// </summary>
    public string BrandName { get; }

    /// <summary>
    /// Gets the normalized primary CSS color.
    /// </summary>
    public string PrimaryColor { get; }

    /// <summary>
    /// Gets the normalized accent CSS color.
    /// </summary>
    public string AccentColor { get; }

    /// <summary>
    /// Gets the normalized background CSS color.
    /// </summary>
    public string BackgroundColor { get; }

    /// <summary>
    /// Gets the normalized text CSS color.
    /// </summary>
    public string TextColor { get; }

    /// <summary>
    /// Gets the canonical heading font family.
    /// </summary>
    public string HeadingFontFamily { get; }

    /// <summary>
    /// Gets the canonical body font family.
    /// </summary>
    public string BodyFontFamily { get; }

    /// <summary>
    /// Gets the optional sanitized logo URI.
    /// </summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Branding settings support root-relative paths and absolute HTTPS URIs.")]
    public string? LogoUri { get; }

    /// <summary>
    /// Creates validated branding settings from a request.
    /// </summary>
    /// <param name="request">The branding settings request.</param>
    /// <param name="allowedFontFamilies">The allowed font families, using each value as its canonical casing.</param>
    /// <returns>A result containing validated branding settings, or validation errors.</returns>
    public static Result<BrandingSettings> Create(BrandingSettingsDto request, IReadOnlyCollection<string> allowedFontFamilies)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(allowedFontFamilies);

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var allowedFonts = BuildAllowedFontMap(allowedFontFamilies);

        var brandName = ValidateBrandName(request.BrandName, errors);
        var primaryColor = ValidateColor(request.PrimaryColor, nameof(request.PrimaryColor), errors);
        var accentColor = ValidateColor(request.AccentColor, nameof(request.AccentColor), errors);
        var backgroundColor = ValidateColor(request.BackgroundColor, nameof(request.BackgroundColor), errors);
        var textColor = ValidateColor(request.TextColor, nameof(request.TextColor), errors);
        var headingFontFamily = ValidateFontFamily(request.HeadingFontFamily, nameof(request.HeadingFontFamily), allowedFonts, errors);
        var bodyFontFamily = ValidateFontFamily(request.BodyFontFamily, nameof(request.BodyFontFamily), allowedFonts, errors);
        var logoUri = ValidateLogoUri(request.LogoUri, errors);

        if (errors.Count > 0)
        {
            return Result.Invalid<BrandingSettings>(ValidationErrorDetail, errors);
        }

        return Result.Ok(new BrandingSettings(
            brandName,
            primaryColor,
            accentColor,
            backgroundColor,
            textColor,
            headingFontFamily,
            bodyFontFamily,
            logoUri));
    }

    /// <summary>
    /// Converts the validated settings to a transport DTO.
    /// </summary>
    /// <returns>The branding settings DTO.</returns>
    public BrandingSettingsDto ToDto() => new()
    {
        BrandName = BrandName,
        PrimaryColor = PrimaryColor,
        AccentColor = AccentColor,
        BackgroundColor = BackgroundColor,
        TextColor = TextColor,
        HeadingFontFamily = HeadingFontFamily,
        BodyFontFamily = BodyFontFamily,
        LogoUri = LogoUri,
    };

    private static string ValidateBrandName(string value, Dictionary<string, string[]> errors)
    {
        var sanitized = StringSanitizer.Sanitize(value) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            errors[nameof(BrandingSettingsDto.BrandName)] = ["Brand name is required."];
            return string.Empty;
        }

        if (sanitized.Length > BrandingContractConstants.MaxBrandNameLength)
        {
            errors[nameof(BrandingSettingsDto.BrandName)] = [$"Brand name must be {BrandingContractConstants.MaxBrandNameLength} characters or fewer."];
            return string.Empty;
        }

        if (ContainsControlCharacter(sanitized))
        {
            errors[nameof(BrandingSettingsDto.BrandName)] = ["Brand name contains unsupported control characters."];
            return string.Empty;
        }

        return sanitized;
    }

    private static string ValidateColor(string value, string fieldName, Dictionary<string, string[]> errors)
    {
        var sanitized = StringSanitizer.Sanitize(value) ?? string.Empty;

        if (sanitized.Length != BrandingContractConstants.MaxCssColorLength || !IsSafeHexColor(sanitized))
        {
            errors[fieldName] = ["Color must use #RRGGBB hexadecimal format."];
            return string.Empty;
        }

        return sanitized.ToUpperInvariant();
    }

    private static string ValidateFontFamily(
        string value,
        string fieldName,
        Dictionary<string, string> allowedFonts,
        Dictionary<string, string[]> errors)
    {
        var sanitized = StringSanitizer.Sanitize(value) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            errors[fieldName] = ["Font family is required."];
            return string.Empty;
        }

        if (sanitized.Length > BrandingContractConstants.MaxFontFamilyLength)
        {
            errors[fieldName] = [$"Font family must be {BrandingContractConstants.MaxFontFamilyLength} characters or fewer."];
            return string.Empty;
        }

        if (!allowedFonts.TryGetValue(sanitized, out var canonicalFontFamily))
        {
            errors[fieldName] = ["Font family is not allowed."];
            return string.Empty;
        }

        return canonicalFontFamily;
    }

    private static string? ValidateLogoUri(string? value, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > BrandingContractConstants.MaxLogoUriLength)
        {
            errors[nameof(BrandingSettingsDto.LogoUri)] = [$"Logo URI must be {BrandingContractConstants.MaxLogoUriLength} characters or fewer."];
            return null;
        }

        var normalized = WebAssetUriSanitizer.NormalizeRootRelativeOrHttps(value, BrandingContractConstants.MaxLogoUriLength);
        if (normalized is null)
        {
            errors[nameof(BrandingSettingsDto.LogoUri)] = ["Logo URI must be root-relative or absolute HTTPS."];
            return null;
        }

        return normalized;
    }

    private static Dictionary<string, string> BuildAllowedFontMap(IEnumerable<string> allowedFontFamilies)
    {
        var allowedFonts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fontFamily in allowedFontFamilies)
        {
            var sanitized = StringSanitizer.Sanitize(fontFamily);
            if (string.IsNullOrWhiteSpace(sanitized) || sanitized.Length > BrandingContractConstants.MaxFontFamilyLength)
            {
                continue;
            }

            allowedFonts.TryAdd(sanitized, sanitized);
        }

        return allowedFonts;
    }

    private static bool IsSafeHexColor(string value)
    {
        if (value[0] != '#')
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            if (!Uri.IsHexDigit(value[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsControlCharacter(string value) => value.Any(char.IsControl);
}
