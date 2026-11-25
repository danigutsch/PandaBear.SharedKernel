namespace SharedKernel.Blazor;

/// <summary>The HTML ids of form controls, built from a prefix and the field path the server names in its errors.</summary>
public static class FieldIds
{
    /// <summary>
    /// Gets the id of the control of a field: the prefix, a hyphen and the field path with its dots and list indexes as hyphens, so a
    /// nested field stays a plain id that a CSS selector and a fragment link can name.
    /// </summary>
    /// <param name="idPrefix">The prefix of the control ids, such as <c>site-tour</c>.</param>
    /// <param name="field">The field path, such as <c>Name</c>, <c>pt.title</c> or <c>pt.itinerary[0].title</c>.</param>
    /// <returns>The id, such as <c>site-tour-Name</c>, <c>site-tour-pt-title</c> or <c>site-tour-pt-itinerary-0-title</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="idPrefix" /> or <paramref name="field" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException"><paramref name="idPrefix" /> is empty or white space.</exception>
    public static string For(string idPrefix, string field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idPrefix);
        ArgumentNullException.ThrowIfNull(field);
        return idPrefix + "-" + field.Replace('.', '-').Replace('[', '-').Replace("]", string.Empty, StringComparison.Ordinal);
    }
}
