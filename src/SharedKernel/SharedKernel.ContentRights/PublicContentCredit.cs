using PandaBear.BuildingBlocks;

namespace PandaBear.ContentRights;

/// <summary>The fields a consumer can use to display credit for a work after deciding to publish them.</summary>
public sealed class PublicContentCredit
{
    /// <summary>Initializes the display fields from validated data.</summary>
    /// <param name="title">The supplied original title.</param>
    /// <param name="parties">The parties named in the credit.</param>
    /// <param name="originalSourceUrl">The original source URL.</param>
    /// <param name="creditLine">The requested credit line.</param>
    /// <param name="copyrightNotice">The supplied copyright notice.</param>
    /// <param name="modificationNotice">The current modification notice.</param>
    /// <param name="previousModificationNotices">Earlier modification notices.</param>
    /// <param name="license">The recorded rights declaration.</param>
    internal PublicContentCredit(
        string? title,
        IReadOnlyList<ContentCreditParty> parties,
        WebUrl? originalSourceUrl,
        string? creditLine,
        string? copyrightNotice,
        string? modificationNotice,
        IReadOnlyList<string> previousModificationNotices,
        ContentLicense license)
    {
        Title = title;
        Parties = parties;
        OriginalSourceUrl = originalSourceUrl;
        CreditLine = creditLine;
        CopyrightNotice = copyrightNotice;
        ModificationNotice = modificationNotice;
        PreviousModificationNotices = previousModificationNotices;
        License = license;
    }

    /// <summary>Gets the supplied title of the original work, if present.</summary>
    public string? Title { get; }

    /// <summary>Gets the explicitly credited parties in display order.</summary>
    public IReadOnlyList<ContentCreditParty> Parties { get; }

    /// <summary>Gets the original source address, if present.</summary>
    public WebUrl? OriginalSourceUrl { get; }

    /// <summary>Gets the requested credit line, if present.</summary>
    public string? CreditLine { get; }

    /// <summary>Gets the supplied copyright notice, if present.</summary>
    public string? CopyrightNotice { get; }

    /// <summary>Gets the notice about changes made in the current use, if present.</summary>
    public string? ModificationNotice { get; }

    /// <summary>Gets the supplied notices about earlier changes.</summary>
    public IReadOnlyList<string> PreviousModificationNotices { get; }

    /// <summary>Gets the declared rights status and available terms.</summary>
    public ContentLicense License { get; }
}
