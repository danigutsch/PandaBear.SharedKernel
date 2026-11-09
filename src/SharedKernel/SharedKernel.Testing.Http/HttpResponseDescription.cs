using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace PandaBear.Testing.Http;

/// <summary>Describes a response body for assertion failure messages without exposing more than a short excerpt.</summary>
internal static class HttpResponseDescription
{
    /// <summary>The largest number of body characters that a failure message quotes.</summary>
    internal const int MaxExcerptCharacters = 200;

    /// <summary>Formats a status code as its number and name, such as <c>404 (NotFound)</c>.</summary>
    /// <param name="status">The status code.</param>
    /// <returns>The formatted status.</returns>
    internal static string Status(HttpStatusCode status) => $"{(int)status} ({status})";

    /// <summary>Describes the body: a truncated excerpt of a text body, or only the size of a binary body.</summary>
    /// <param name="response">The response, which remains owned by the caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>&lt;empty&gt;</c>, a single-line excerpt, or the byte count and media type of a non-text body.</returns>
    internal static async Task<string> Body(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (bytes.Length == 0)
        {
            return "<empty>";
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!IsText(mediaType))
        {
            return $"<{bytes.Length} bytes of {mediaType ?? "unknown content type"}>";
        }

        var decodedBytes = Math.Min(bytes.Length, MaxExcerptCharacters * 4);
        var text = Encoding.UTF8.GetString(bytes, 0, decodedBytes);
        var characters = text.Length > MaxExcerptCharacters ? text[..MaxExcerptCharacters] : text;
        var excerpt = new StringBuilder(characters.Length + 48);
        foreach (var character in characters)
        {
            excerpt.Append(char.IsControl(character) ? ' ' : character);
        }

        if (decodedBytes < bytes.Length || text.Length > MaxExcerptCharacters)
        {
            excerpt.Append(CultureInfo.InvariantCulture, $"... (truncated, {bytes.Length} bytes in total)");
        }

        return excerpt.ToString();
    }

    /// <summary>Formats the first sentence of a content type failure; parameters such as <c>charset</c> are not quoted.</summary>
    /// <param name="expectedMediaType">The expected media type.</param>
    /// <param name="actual">The actual content type header, or null.</param>
    /// <returns>A sentence such as <c>Expected content type 'application/json' but was 'text/plain'.</c></returns>
    internal static string ContentTypeDifference(string expectedMediaType, MediaTypeHeaderValue? actual) =>
        actual?.MediaType is { } mediaType
            ? $"Expected content type '{expectedMediaType}' but was '{mediaType}'."
            : $"Expected content type '{expectedMediaType}' but the response has none.";

    private static bool IsText(string? mediaType) =>
        mediaType is not null
        && (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            || mediaType.EndsWith("json", StringComparison.OrdinalIgnoreCase)
            || mediaType.EndsWith("xml", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase));
}
