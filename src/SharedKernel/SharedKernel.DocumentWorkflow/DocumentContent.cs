using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using SharedKernel.DocumentRendering;

namespace SharedKernel.DocumentWorkflow;

/// <summary>Contains one immutable structure presented for document review and rendering.</summary>
public sealed class DocumentContent
{
    /// <summary>Creates a document section from validated fields and captured branding.</summary>
    /// <param name="language">The content language.</param>
    /// <param name="title">The document title.</param>
    /// <param name="sectionHeading">The heading shown above the fields.</param>
    /// <param name="fields">The structured fields in rendering order.</param>
    /// <param name="branding">The optional captured branding.</param>
    /// <exception cref="ArgumentException">Thrown when structural text or a field is invalid, or field identifiers repeat.</exception>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
    public DocumentContent(
        string language,
        string title,
        string sectionHeading,
        IEnumerable<DocumentContentField> fields,
        DocumentBrandingSnapshot? branding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionHeading);
        ArgumentNullException.ThrowIfNull(fields);

        var snapshot = fields.ToImmutableArray();
        if (snapshot.IsEmpty || snapshot.Any(static field =>
                field is null || string.IsNullOrWhiteSpace(field.Id) || string.IsNullOrWhiteSpace(field.Label) ||
                field.Value is null || field.PrivacyClassification == DocumentPrivacyClassification.None ||
                !Enum.IsDefined(field.PrivacyClassification)) ||
            snapshot.Select(static field => field.Id).Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
        {
            throw new ArgumentException("Document fields require unique identifiers, labels and privacy classifications.", nameof(fields));
        }

        Language = language;
        Title = title;
        SectionHeading = sectionHeading;
        Fields = snapshot;
        Branding = branding;
    }

    /// <summary>Gets the language presented for review.</summary>
    public string Language { get; }

    /// <summary>Gets the document title.</summary>
    public string Title { get; }

    /// <summary>Gets the heading shown above the fields.</summary>
    public string SectionHeading { get; }

    /// <summary>Gets the ordered and classified field snapshot.</summary>
    public ImmutableArray<DocumentContentField> Fields { get; }

    /// <summary>Gets the branding captured while generating the draft.</summary>
    public DocumentBrandingSnapshot? Branding { get; }

    /// <summary>Projects the reviewed snapshot to the existing renderer contract.</summary>
    /// <returns>A structured request for a document renderer.</returns>
    public DocumentRenderRequest ToRenderRequest() =>
        new(Language, Title, [new DocumentSection(SectionHeading, Fields
            .Select(static field => new DocumentField(field.Label, field.Value, field.PrivacyClassification)).ToImmutableArray())], Branding);

    /// <summary>Replaces one field while preserving the document structure and branding.</summary>
    internal DocumentContent WithField(int index, string value) =>
        new(Language, Title, SectionHeading, Fields.SetItem(index, Fields[index] with { Value = value }), Branding);

    /// <summary>Computes the review digest from the version-one structured content encoding.</summary>
    internal string CalculateHash(Guid lineageId, int number, DocumentOrigin origin)
    {
        var fingerprint = new DocumentReviewFingerprint(lineageId, number, origin, this);
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            fingerprint, DocumentContentJsonContext.Default.DocumentReviewFingerprint)));
    }
}
