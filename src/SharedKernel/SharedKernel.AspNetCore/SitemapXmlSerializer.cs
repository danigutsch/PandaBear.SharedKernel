using System.Xml;
using System.Xml.Linq;

namespace SharedKernel.AspNetCore;

/// <summary>
/// Serializes sitemap entries using the sitemap XML protocol.
/// </summary>
public static class SitemapXmlSerializer
{
    /// <summary>
    /// The maximum number of URL entries allowed by the sitemap protocol.
    /// </summary>
    public const int MaximumUrlCount = 50_000;

    /// <summary>
    /// The response content type for sitemap XML documents.
    /// </summary>
    public const string ContentType = "application/xml; charset=utf-8";

    private static readonly XNamespace SitemapNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";

    /// <summary>
    /// Serializes URL entries into a sitemap XML document.
    /// </summary>
    /// <param name="entries">The sitemap URL entries.</param>
    /// <param name="cancellationToken">The token used to cancel serialization.</param>
    /// <returns>A task that represents the serialization operation. The task result contains the UTF-8 encoded sitemap XML document.</returns>
    /// <exception cref="InvalidOperationException">Thrown when more than <see cref="MaximumUrlCount" /> entries are provided.</exception>
    public static async Task<byte[]> Serialize(IEnumerable<SitemapEntry> entries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var urls = new List<XElement>();
        foreach (var entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry);

            if (urls.Count == MaximumUrlCount)
            {
                throw new InvalidOperationException($"Sitemap documents cannot contain more than {MaximumUrlCount} URLs.");
            }

            urls.Add(CreateSitemapUrl(entry));
        }

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(SitemapNamespace + "urlset", urls));

        using var stream = new MemoryStream();
        await document.SaveAsync(stream, SaveOptions.None, cancellationToken).ConfigureAwait(false);
        return stream.ToArray();
    }

    private static XElement CreateSitemapUrl(SitemapEntry entry)
    {
        var url = new XElement(
            SitemapNamespace + "url",
            new XElement(SitemapNamespace + "loc", entry.Location.AbsoluteUri));

        if (entry.LastModified is { } timestamp && timestamp != default)
        {
            url.Add(new XElement(
                SitemapNamespace + "lastmod",
                XmlConvert.ToString(timestamp.UtcDateTime, XmlDateTimeSerializationMode.Utc)));
        }

        return url;
    }
}
