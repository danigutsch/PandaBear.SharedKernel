using System.Collections.Immutable;
using SharedKernel.Review;

namespace SharedKernel.DocumentWorkflow.EntityFrameworkCore;

/// <summary>Defines the version-one persisted content for a document revision.</summary>
/// <param name="Origin">The captured source and template provenance.</param>
/// <param name="Language">The document language.</param>
/// <param name="Title">The document title.</param>
/// <param name="SectionHeading">The field section title.</param>
/// <param name="Fields">The ordered classified fields.</param>
/// <param name="Branding">The captured branding.</param>
/// <param name="Review">The current review version and evidence.</param>
/// <param name="Status">The stored lifecycle status.</param>
internal sealed record DocumentRevisionPayload(
    DocumentOrigin Origin, string Language, string Title, string SectionHeading,
    ImmutableArray<DocumentContentField> Fields, DocumentBrandingPayload? Branding,
    ReviewHistorySnapshot Review, DocumentLifecycleStatus Status)
{
    /// <summary>Projects a validated snapshot onto the version-one wire shape.</summary>
    public static DocumentRevisionPayload From(DocumentRevisionSnapshot snapshot) => new(
        snapshot.Origin, snapshot.Content.Language, snapshot.Content.Title, snapshot.Content.SectionHeading,
        snapshot.Content.Fields, DocumentBrandingPayload.From(snapshot.Content.Branding), snapshot.Review, snapshot.Status);

    /// <summary>Reconstructs the typed workflow snapshot for validation.</summary>
    public DocumentRevisionSnapshot ToSnapshot(Guid lineageId, int number, string? artifactReference) =>
        new(lineageId, number, Origin, new DocumentContent(Language, Title, SectionHeading, Fields, Branding?.ToBranding()),
            Review, Status, artifactReference);
}
