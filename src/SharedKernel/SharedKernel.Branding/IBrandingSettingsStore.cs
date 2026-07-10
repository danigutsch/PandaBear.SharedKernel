namespace SharedKernel.Branding;

/// <summary>
/// Persists validated branding settings without imposing a storage provider.
/// </summary>
public interface IBrandingSettingsStore
{
    /// <summary>
    /// Gets the current validated branding settings, when present.
    /// </summary>
    /// <param name="ct">Cancellation token for the storage operation.</param>
    /// <returns>The validated branding settings, or <see langword="null" /> when none exist.</returns>
    Task<BrandingSettings?> GetSettings(CancellationToken ct);

    /// <summary>
    /// Saves validated branding settings.
    /// </summary>
    /// <param name="settings">The validated branding settings.</param>
    /// <param name="ct">Cancellation token for the storage operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveSettings(BrandingSettings settings, CancellationToken ct);
}
