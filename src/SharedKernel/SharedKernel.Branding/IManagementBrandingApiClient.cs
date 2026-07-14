namespace SharedKernel.Branding;

/// <summary>
/// Defines authenticated Management Web operations for Branding API contracts.
/// </summary>
public interface IManagementBrandingApiClient
{
    /// <summary>
    /// Gets management branding settings.
    /// </summary>
    /// <param name="ct">Cancellation token for the request.</param>
    /// <returns>The management branding settings.</returns>
    Task<BrandingSettingsDto> GetSettings(CancellationToken ct);

    /// <summary>
    /// Saves branding settings.
    /// </summary>
    /// <param name="request">The branding settings request.</param>
    /// <param name="ct">Cancellation token for the request.</param>
    /// <returns>The saved branding settings.</returns>
    Task<BrandingSettingsDto> SaveSettings(BrandingSettingsDto request, CancellationToken ct);
}
