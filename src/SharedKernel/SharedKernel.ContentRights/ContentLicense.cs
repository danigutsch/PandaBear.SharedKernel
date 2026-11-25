using System.Diagnostics.CodeAnalysis;
using PandaBear.BuildingBlocks;
using PandaBear.Results;

namespace PandaBear.ContentRights;

/// <summary>A recorded license or rights statement for a work, with its supplied terms and recognized standard identity.</summary>
public sealed class ContentLicense
{
    /// <summary>Gets the declaration used when no rights data is supplied.</summary>
    internal static ContentLicense Unknown { get; } = new(ContentLicenseKind.Unknown, null, null, null, null, null, null);

    private ContentLicense(
        ContentLicenseKind kind, string? declaredTerms, string? declaredTermsUrl, WebUrl? termsUrl,
        WebUrl? canonicalTermsUrl, string? version, string? jurisdiction)
    {
        Kind = kind;
        DeclaredTerms = declaredTerms;
        DeclaredTermsUrl = declaredTermsUrl;
        TermsUrl = termsUrl;
        CanonicalTermsUrl = canonicalTermsUrl;
        Version = version;
        Jurisdiction = jurisdiction;
    }

    /// <summary>Gets the explicitly declared rights status or license family.</summary>
    public ContentLicenseKind Kind { get; }

    /// <summary>Gets the supplied wording about the terms, if present.</summary>
    public string? DeclaredTerms { get; }

    /// <summary>Gets the terms address as recorded, before URL canonicalization.</summary>
    [SuppressMessage("Design", "CA1056:URI properties should not be strings",
        Justification = "Preserves the supplied spelling alongside the validated WebUrl.")]
    public string? DeclaredTermsUrl { get; }

    /// <summary>Gets the validated terms address, retaining the supplied path, query, and fragment.</summary>
    public WebUrl? TermsUrl { get; }

    /// <summary>Gets the HTTPS deed address of the explicitly declared standard tool, when recognized.</summary>
    public WebUrl? CanonicalTermsUrl { get; }

    /// <summary>Gets the version identified by the recorded Creative Commons terms address, if recognized.</summary>
    public string? Version { get; }

    /// <summary>Gets the jurisdiction or port identifier in the recognized Creative Commons address, if present.</summary>
    public string? Jurisdiction { get; }

    /// <summary>Records the declared kind and optional original terms without inferring rights from text or a URL.</summary>
    /// <param name="kind">The explicitly declared license family or other rights statement.</param>
    /// <param name="declaredTerms">The supplied wording about the terms.</param>
    /// <param name="termsUrl">The recorded absolute address of the terms.</param>
    /// <returns>The recorded declaration or field validation errors.</returns>
    [SuppressMessage("Design", "CA1054:URI parameters should not be strings",
        Justification = "Validates the exact input and retains its original spelling.")]
    public static Result<ContentLicense> Create(ContentLicenseKind kind, string? declaredTerms = null, string? termsUrl = null)
    {
        var errors = new ValidationErrors();
        if (!Enum.IsDefined(kind))
        {
            AttributionValidation.Add(errors, "kind", "License kind is not defined.");
        }

        var wording = AttributionValidation.Text(errors, declaredTerms, "declaredTerms", "Declared terms");
        var terms = AttributionValidation.Url(errors, termsUrl, "termsUrl", "Terms URL");
        WebUrl? canonical = null;
        string? version = null;
        string? jurisdiction = null;
        if (terms is not null && CreativeCommonsTerms.IsStandardTool(kind)
            && !CreativeCommonsTerms.TryIdentify(kind, terms, out canonical, out version, out jurisdiction))
        {
            AttributionValidation.Add(errors, "termsUrl", "Terms URL must identify the declared Creative Commons tool.");
        }

        return errors.HasErrors
            ? errors.ToResult<ContentLicense>()
            : Result.Ok(new ContentLicense(kind, wording, termsUrl, terms, canonical, version, jurisdiction));
    }
}
