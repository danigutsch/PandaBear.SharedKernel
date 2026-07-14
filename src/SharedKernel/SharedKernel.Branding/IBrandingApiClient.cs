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

}
