namespace PandaBear.AspNetCore;

/// <summary>Keeps the route template tag bounded: a template, never a URL, query string or credential.</summary>
internal static class RouteTemplateNormalizer
{
    private const int MaximumLength = 256;
    private static readonly System.Buffers.SearchValues<char> SchemeCharacters =
        System.Buffers.SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789+-.");

    /// <summary>Normalizes a caller-supplied route template.</summary>
    /// <param name="value">The value supplied by the caller.</param>
    /// <returns>A bounded template, or the unmatched marker.</returns>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return AspNetCoreTelemetry.UnmatchedRoute;
        }

        var end = value.AsSpan().IndexOfAny('?', '#');
        var template = (end < 0 ? value : value[..end]).Trim();
        if (template.Length is 0 or > MaximumLength || LooksLikeUri(template))
        {
            return AspNetCoreTelemetry.UnmatchedRoute;
        }

        return template;
    }

    /// <remarks>
    /// A scheme is letters, digits, '+', '-' or '.' before the first ':'; route constraints such as {id:int}
    /// are preceded by '/' or '{', which a scheme cannot contain.
    /// </remarks>
    private static bool LooksLikeUri(string template)
    {
        if (template.Contains("://", StringComparison.Ordinal))
        {
            return true;
        }

        var colon = template.IndexOf(':', StringComparison.Ordinal);
        return colon > 0 && char.IsAsciiLetter(template[0])
            && template.AsSpan(0, colon).IndexOfAnyExcept(SchemeCharacters) < 0;
    }
}
