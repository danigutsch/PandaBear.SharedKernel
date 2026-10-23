namespace PandaBear.VaultManager.Tool.Model;

/// <summary>
/// Masks the parts of a website address that can hold credentials or tokens: user info, the query, and the fragment. The
/// scheme, host, and path stay readable.
/// </summary>
internal static class UriMasking
{
    /// <summary>Masks the user info, the query, and the fragment of an address.</summary>
    /// <param name="uri">Address as stored; it need not be a valid absolute URI.</param>
    /// <param name="mask">Text that replaces each masked part.</param>
    /// <returns>The address with user info replaced before <c>@</c>, and the text after <c>?</c> or <c>#</c> replaced.</returns>
    public static string Mask(string uri, string mask)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(mask);

        int scheme = uri.IndexOf("://", StringComparison.Ordinal);
        int authorityStart = scheme < 0 ? 0 : scheme + 3;
        int authorityEnd = uri.IndexOfAny(['/', '?', '#'], authorityStart);
        authorityEnd = authorityEnd < 0 ? uri.Length : authorityEnd;
        int at = authorityEnd > authorityStart ? uri.LastIndexOf('@', authorityEnd - 1, authorityEnd - authorityStart) : -1;
        string authority = at < 0 ? uri[authorityStart..authorityEnd] : mask + uri[at..authorityEnd];
        int rest = uri.IndexOfAny(['?', '#'], authorityEnd);
        string path = rest < 0 ? uri[authorityEnd..] : uri[authorityEnd..rest] + uri[rest] + mask;
        return uri[..authorityStart] + authority + path;
    }
}
