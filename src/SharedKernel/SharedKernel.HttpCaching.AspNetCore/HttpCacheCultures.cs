namespace SharedKernel.HttpCaching.AspNetCore;

/// <summary>
/// Provides canonical culture values used by HTTP cache query-key normalization.
/// </summary>
public static class HttpCacheCultures
{
    /// <summary>
    /// Normalizes supported culture aliases to their canonical cache-key value.
    /// </summary>
    /// <param name="culture">The culture or language alias to normalize.</param>
    /// <returns>The canonical culture value, or <see langword="null" /> when unsupported.</returns>
    public static string? Normalize(string? culture)
    {
        return culture?.Trim().ToUpperInvariant() switch
        {
            "EN-US" or "EN" => "en-US",
            "PT-BR" or "PT" => "pt-BR",
            _ => null
        };
    }
}
