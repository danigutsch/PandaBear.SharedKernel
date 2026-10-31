namespace PandaBear.VaultManager.Tool.Model;

/// <summary>
/// Masks the parts of a website address that can hold credentials or tokens: user info, the query, and the fragment. The
/// scheme, host, and path stay readable.
/// </summary>
internal static class UriMasking
{
    /// <summary>
    /// Masks the user info, the query, and the fragment of an address. The authority ends at the first <c>/</c>,
    /// <c>\</c>, <c>?</c>, or <c>#</c>, as browsers read it. When an <c>@</c> comes after that, the user info itself may
    /// hold one of those characters, so where the host starts is unclear, and everything after the scheme is masked.
    /// </summary>
    /// <param name="uri">Address as stored; it need not be a valid absolute URI.</param>
    /// <param name="mask">Text that replaces each masked part.</param>
    /// <returns>
    /// The address with user info replaced before <c>@</c>, and the text after <c>?</c> or <c>#</c> replaced; or the scheme
    /// followed by the mask.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="uri"/> or <paramref name="mask"/> is <see langword="null"/>.</exception>
    public static string Mask(string uri, string mask)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(mask);

        int authorityStart = SchemeLength(uri);
        int authorityEnd = uri.IndexOfAny(['/', '\\', '?', '#'], authorityStart);
        authorityEnd = authorityEnd < 0 ? uri.Length : authorityEnd;
        if (uri.IndexOf('@', authorityEnd) >= 0)
        {
            return MaskAfterScheme(uri, mask);
        }

        int at = authorityEnd > authorityStart ? uri.LastIndexOf('@', authorityEnd - 1, authorityEnd - authorityStart) : -1;
        string authority = at < 0 ? uri[authorityStart..authorityEnd] : mask + uri[at..authorityEnd];
        int rest = uri.IndexOfAny(['?', '#'], authorityEnd);
        string path = rest < 0 ? uri[authorityEnd..] : uri[authorityEnd..rest] + uri[rest] + mask;
        return uri[..authorityStart] + authority + path;
    }

    /// <summary>Masks everything after the scheme of an address, such as one that holds a secret in its path.</summary>
    /// <param name="uri">Address as stored; it need not be a valid absolute URI.</param>
    /// <param name="mask">Text that replaces the rest of the address.</param>
    /// <returns>The scheme and <c>://</c> followed by the mask, or the mask alone for an address without a scheme.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="uri"/> or <paramref name="mask"/> is <see langword="null"/>.</exception>
    public static string MaskAfterScheme(string uri, string mask)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(mask);

        return uri[..SchemeLength(uri)] + mask;
    }

    // Length of a leading "scheme://": a letter, then letters, digits, '+', '-', or '.'. A "://" later in the address, such
    // as in a query, starts no scheme.
    private static int SchemeLength(string uri)
    {
        int separator = uri.IndexOf("://", StringComparison.Ordinal);
        if (separator <= 0 || !char.IsAsciiLetter(uri[0]))
        {
            return 0;
        }

        for (int index = 1; index < separator; index++)
        {
            char character = uri[index];
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('+' or '-' or '.'))
            {
                return 0;
            }
        }

        return separator + 3;
    }
}
