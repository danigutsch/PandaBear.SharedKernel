using Microsoft.AspNetCore.Http;

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

    /// <summary>
    /// Rewrites culture and language aliases to a canonical query key for cache-key stability.
    /// </summary>
    /// <param name="httpContext">The request context whose query string is normalized.</param>
    /// <param name="cultureQueryKey">The canonical culture query-string key.</param>
    /// <param name="languageQueryKey">The alternate language query-string key.</param>
    /// <param name="invalidCultureValue">The optional cache-key value used when an explicit culture is unsupported.</param>
    public static void NormalizeQueryAliases(
        HttpContext httpContext,
        string cultureQueryKey,
        string languageQueryKey,
        string? invalidCultureValue = null)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureQueryKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageQueryKey);

        var hasCultureInput = httpContext.Request.Query.ContainsKey(cultureQueryKey)
            || httpContext.Request.Query.ContainsKey(languageQueryKey);
        if (!hasCultureInput)
        {
            return;
        }

        var rawCulture = httpContext.Request.Query.TryGetValue(cultureQueryKey, out var cultureValue)
            ? cultureValue.ToString()
            : null;
        var hasCultureValue = !string.IsNullOrWhiteSpace(rawCulture);
        var canonicalCulture = Normalize(rawCulture);

        var rawLanguage = httpContext.Request.Query.TryGetValue(languageQueryKey, out var language)
            ? language.ToString()
            : null;
        var hasLanguageValue = !string.IsNullOrWhiteSpace(rawLanguage);

        canonicalCulture ??= Normalize(rawLanguage);

        var queryValues = new List<KeyValuePair<string, string?>>();
        foreach (var (key, values) in httpContext.Request.Query)
        {
            if (key.Equals(languageQueryKey, StringComparison.OrdinalIgnoreCase)
                || key.Equals(cultureQueryKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var value in values)
            {
                queryValues.Add(new KeyValuePair<string, string?>(key, value));
            }
        }

        if (canonicalCulture is not null)
        {
            queryValues.Add(new KeyValuePair<string, string?>(cultureQueryKey, canonicalCulture));
        }
        else if (invalidCultureValue is not null && (hasCultureValue || hasLanguageValue))
        {
            queryValues.Add(new KeyValuePair<string, string?>(cultureQueryKey, invalidCultureValue));
        }

        httpContext.Request.QueryString = QueryString.Create(queryValues);
    }
}
