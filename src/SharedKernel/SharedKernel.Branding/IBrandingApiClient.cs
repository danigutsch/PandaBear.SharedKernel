namespace SharedKernel.Branding;

/// <summary>
/// Defines client operations for branding API contracts.
/// </summary>
public interface IBrandingApiClient
{
    /// <summary>
    /// Gets public branding settings.
    /// </summary>
    /// <param name="ct">Cancellation token for the request.</param>
    /// <returns>The public branding settings.</returns>
    Task<BrandingSettingsDto> GetPublicSettings(CancellationToken ct);

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
