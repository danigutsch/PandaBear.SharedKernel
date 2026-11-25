using System.Diagnostics.CodeAnalysis;

namespace SharedKernel.ContentRights;

/// <summary>Recorded inputs intended for public attribution after the caller reviews them.</summary>
public sealed class ContentAttributionDefinition
{
    /// <summary>Gets or initializes the title of the original work, when supplied.</summary>
    public string? Title { get; init; }

    /// <summary>Gets or initializes the publicly credited parties in display order.</summary>
    public IReadOnlyList<ContentCreditParty>? Parties { get; init; }

    /// <summary>Gets or initializes the absolute address of the original work or the page where it was obtained.</summary>
    [SuppressMessage("Design", "CA1056:URI properties should not be strings",
        Justification = "Captures the entered string for validation and exact provenance.")]
    public string? OriginalSourceUrl { get; init; }

    /// <summary>Gets or initializes the public credit line requested by the source.</summary>
    public string? CreditLine { get; init; }

    /// <summary>Gets or initializes the supplied copyright notice.</summary>
    public string? CopyrightNotice { get; init; }

    /// <summary>Gets or initializes the supplied notice about changes made in the current use.</summary>
    public string? ModificationNotice { get; init; }

    /// <summary>Gets or initializes the supplied notices about earlier changes in display order.</summary>
    public IReadOnlyList<string>? PreviousModificationNotices { get; init; }

    /// <summary>Gets or initializes the declared terms. An absent declaration records unknown rights.</summary>
    public ContentLicense? License { get; init; }
}
