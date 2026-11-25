using SharedKernel.BuildingBlocks;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using SharedKernel.Results;

namespace SharedKernel.ContentRights;

/// <summary>Validated credit and rights declarations recorded for a piece of content.</summary>
public sealed class ContentAttribution
{
    /// <summary>The maximum length, in UTF-16 code units, of each field of credit, notice, or terms text.</summary>
    public const int MaxTextLength = 1024;

    /// <summary>The maximum number of explicitly credited parties.</summary>
    public const int MaxCreditParties = 20;

    /// <summary>The maximum number of retained earlier modification notices.</summary>
    public const int MaxPreviousModificationNotices = 20;

    private readonly PublicContentCredit _credit;

    private ContentAttribution(string? declaredOriginalSourceUrl, PublicContentCredit credit)
    {
        DeclaredOriginalSourceUrl = declaredOriginalSourceUrl;
        _credit = credit;
    }

    /// <summary>Gets the original source address as recorded, before URL canonicalization.</summary>
    [SuppressMessage("Design", "CA1056:URI properties should not be strings",
        Justification = "Preserves the supplied spelling alongside the validated WebUrl.")]
    public string? DeclaredOriginalSourceUrl { get; }

    /// <summary>Validates and records the supplied credit and rights declaration.</summary>
    /// <param name="definition">The entered credit, source, and rights declaration.</param>
    /// <returns>The recorded attribution or field validation errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is <see langword="null"/>.</exception>
    public static Result<ContentAttribution> Create(ContentAttributionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var errors = new ValidationErrors();
        var title = AttributionValidation.Text(errors, definition.Title, "title", "Work title");
        var parties = ValidateParties(errors, definition.Parties ?? []);
        var originalSource = AttributionValidation.Url(errors, definition.OriginalSourceUrl, "originalSourceUrl", "Original source URL");
        var creditLine = AttributionValidation.Text(errors, definition.CreditLine, "creditLine", "Credit line");
        var copyright = AttributionValidation.Text(errors, definition.CopyrightNotice, "copyrightNotice", "Copyright notice");
        var modification = AttributionValidation.Text(errors, definition.ModificationNotice, "modificationNotice", "Modification notice");
        var previous = ValidatePreviousNotices(errors, definition.PreviousModificationNotices ?? []);

        return errors.HasErrors
            ? errors.ToResult<ContentAttribution>()
            : Result.Ok(new ContentAttribution(
                definition.OriginalSourceUrl,
                new PublicContentCredit(title, parties, originalSource, creditLine, copyright, modification, previous,
                    definition.License ?? ContentLicense.Unknown)));
    }

    /// <summary>Creates an attribution from an existing image credit using a caller-reviewed rights declaration.</summary>
    /// <param name="imageCredit">The existing recorded image credit.</param>
    /// <param name="reviewedKind">The rights declaration established by the caller, or <see cref="ContentLicenseKind.Unknown"/>.</param>
    /// <param name="modificationNotice">The public wording for known changes. Required when the image credit records a modification.</param>
    /// <param name="creatorKind">The author's entity kind, when established by the caller.</param>
    /// <returns>The converted attribution or field validation errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="imageCredit"/> is <see langword="null"/>.</exception>
    public static Result<ContentAttribution> FromImageCredit(
        ImageCredit imageCredit,
        ContentLicenseKind reviewedKind,
        string? modificationNotice = null,
        ContentCreditEntityKind creatorKind = ContentCreditEntityKind.Unknown)
    {
        ArgumentNullException.ThrowIfNull(imageCredit);

        if (imageCredit.Modified && string.IsNullOrWhiteSpace(modificationNotice))
        {
            const string message = "Provide a public notice describing the recorded modification.";
            return Result.Invalid<ContentAttribution>(detail: message, field: "modificationNotice", message: message);
        }

        return ContentLicense.Create(reviewedKind, imageCredit.License.Code, imageCredit.License.Url?.Value)
            .Bind(license => Create(new ContentAttributionDefinition
            {
                Title = imageCredit.Title,
                Parties = [new ContentCreditParty(imageCredit.Author, ContentCreditRole.Creator, creatorKind)],
                OriginalSourceUrl = imageCredit.SourceUrl?.Value,
                ModificationNotice = modificationNotice,
                License = license,
            }));
    }

    /// <summary>Returns the public display fields for a caller to publish after review.</summary>
    /// <returns>The credit, notices, source, and declared rights. The caller retains approval evidence separately.</returns>
    public PublicContentCredit ToPublicCredit() => _credit;

    private static ReadOnlyCollection<ContentCreditParty> ValidateParties(ValidationErrors errors, IReadOnlyList<ContentCreditParty> parties)
    {
        var count = parties.Count;
        if (count > MaxCreditParties)
        {
            AttributionValidation.Add(errors, "parties", "Too many credit parties.");
            return ReadOnlyCollection<ContentCreditParty>.Empty;
        }

        var copy = new List<ContentCreditParty>(count);
        for (var index = 0; index < count; index++)
        {
            var field = string.Create(CultureInfo.InvariantCulture, $"parties[{index}]");
            if (parties[index] is not { } party)
            {
                AttributionValidation.Add(errors, field, "Credit party is required.");
                continue;
            }

            AttributionValidation.Text(errors, party.Name, field + ".name", "Credit party name", required: true);
            if (!Enum.IsDefined(party.Role))
            {
                AttributionValidation.Add(errors, field + ".role", "Credit party role is not defined.");
            }

            if (!Enum.IsDefined(party.EntityKind))
            {
                AttributionValidation.Add(errors, field + ".entityKind", "Credit party entity kind is not defined.");
            }

            copy.Add(party);
        }

        return copy.AsReadOnly();
    }

    private static ReadOnlyCollection<string> ValidatePreviousNotices(ValidationErrors errors, IReadOnlyList<string> notices)
    {
        var count = notices.Count;
        if (count > MaxPreviousModificationNotices)
        {
            AttributionValidation.Add(errors, "previousModificationNotices", "Too many previous modification notices.");
            return ReadOnlyCollection<string>.Empty;
        }

        var copy = new List<string>(count);
        for (var index = 0; index < count; index++)
        {
            var field = string.Create(CultureInfo.InvariantCulture, $"previousModificationNotices[{index}]");
            if (AttributionValidation.Text(errors, notices[index], field, "Previous modification notice", required: true) is { } notice)
            {
                copy.Add(notice);
            }
        }

        return copy.AsReadOnly();
    }
}
