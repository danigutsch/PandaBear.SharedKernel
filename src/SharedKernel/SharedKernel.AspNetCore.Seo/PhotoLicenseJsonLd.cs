using System.Buffers;
using System.Text;
using System.Text.Json;
using PandaBear.BuildingBlocks;
using PandaBear.ContentRights;

namespace PandaBear.AspNetCore.Seo;

/// <summary>Writes Schema.org image metadata from a caller-approved photo URL and public attribution record.</summary>
public static class PhotoLicenseJsonLd
{
    /// <summary>Creates an HTML-script-safe JSON-LD value for one published image.</summary>
    /// <param name="canonicalPublicImageUrl">The actual public URL of the served image, or <see langword="null"/> for an unpublished image.</param>
    /// <param name="credit">The credit approved for public display, or <see langword="null"/> when unavailable.</param>
    /// <param name="acquireLicensePage">An optional public page explaining how to request a license for this image.</param>
    /// <returns>An encoded Schema.org <c>ImageObject</c>, or <see langword="null"/> when the public image or credit is absent.</returns>
    public static string? Serialize(WebUrl? canonicalPublicImageUrl, PublicContentCredit? credit, WebUrl? acquireLicensePage = null)
    {
        if (canonicalPublicImageUrl is null || credit is null)
        {
            return null;
        }

        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartObject();
        writer.WriteString("@context", "https://schema.org");
        writer.WriteString("@type", "ImageObject");
        writer.WriteString("contentUrl", canonicalPublicImageUrl.Value);
        WriteCreators(writer, credit.Parties);
        WriteOptional(writer, "creditText", credit.CreditLine);
        WriteOptional(writer, "copyrightNotice", credit.CopyrightNotice);
        WriteOptional(writer, "license", Terms(credit.License)?.Value);
        WriteOptional(writer, "acquireLicensePage", acquireLicensePage?.Value);
        writer.WriteEndObject();
        writer.Flush();
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteCreators(Utf8JsonWriter writer, IReadOnlyList<ContentCreditParty> parties)
    {
        var creators = parties.Where(static party => party.Role == ContentCreditRole.Creator
            && party.EntityKind is ContentCreditEntityKind.Person or ContentCreditEntityKind.Organization).ToArray();
        if (creators.Length == 0)
        {
            return;
        }

        writer.WriteStartArray("creator");
        foreach (var creator in creators)
        {
            writer.WriteStartObject();
            writer.WriteString("@type", creator.EntityKind == ContentCreditEntityKind.Person ? "Person" : "Organization");
            writer.WriteString("name", creator.Name);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static WebUrl? Terms(ContentLicense license) => license.Kind switch
    {
        ContentLicenseKind.CustomTerms => license.TermsUrl,
        ContentLicenseKind.CcBy or ContentLicenseKind.CcBySa or ContentLicenseKind.CcByNd or ContentLicenseKind.CcByNc
            or ContentLicenseKind.CcByNcSa or ContentLicenseKind.CcByNcNd or ContentLicenseKind.Cc0 => license.CanonicalTermsUrl,
        _ => null,
    };

    private static void WriteOptional(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(name, value);
        }
    }
}
