using System.Text.Json;

namespace SharedKernel.Blazor.Site.Theming;

/// <summary>
/// Writes and reads a stored site theme as one JSON value, the <see cref="ThemeDocument" /> format:
/// <c>{"version":1,"colors":{"primary":{"light":"#1d4ed8","dark":"#93c5fd"}},"typography":{...},"fonts":[...],"allowSmallText":false,"header":{...},"shape":{...}}</c>.
/// Properties are camel case, enums are written by member name (<c>Pill</c>, <c>Serif</c>) and never read from a number, and an empty
/// value is left out. A host keeps the text in one column or file, reads and writes it whole and never queries into it.
/// </summary>
public static class ThemeDocumentJson
{
    /// <summary>Writes a theme as stored: its <see cref="ThemeDocument" /> at <see cref="ThemeDocument.CurrentVersion" />, without indentation.</summary>
    /// <param name="theme">The theme.</param>
    /// <returns>The JSON text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="theme" /> is <see langword="null" />.</exception>
    public static string Serialize(SiteTheme theme) =>
        JsonSerializer.Serialize(ThemeDocument.From(theme), ThemeDocumentJsonContext.Default.ThemeDocument);

    /// <summary>
    /// Reads a stored theme with <see cref="ThemeDocument.Restore" />. Text written before the format was versioned, without a
    /// <c>version</c> member, reads as version 1; a member the format does not define is ignored.
    /// </summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The theme.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json" /> is <see langword="null" />.</exception>
    /// <exception cref="JsonException">The text is not JSON, or a value does not fit the format.</exception>
    /// <exception cref="InvalidOperationException">
    /// The text is the JSON <c>null</c>, has a format version this library does not read, or cannot be read as a theme.
    /// </exception>
    public static SiteTheme Restore(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var document = JsonSerializer.Deserialize(json, ThemeDocumentJsonContext.Default.ThemeDocument)
            ?? throw new InvalidOperationException("The stored site theme is empty.");
        return document.Restore();
    }
}
