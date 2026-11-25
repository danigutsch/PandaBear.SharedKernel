using Microsoft.AspNetCore.Components;
using SharedKernel.BuildingBlocks;
using SharedKernel.ContentRights;

namespace SharedKernel.Blazor.Site;

/// <summary>Displays approved attribution in full or as a compact credit with a link or disclosure.</summary>
public sealed partial class PhotoCredit
{
    /// <summary>Gets or sets the approved credit and contextual labels for the photo.</summary>
    [Parameter]
    [EditorRequired]
    public PhotoCreditItem Item { get; set; } = null!;

    /// <summary>Gets or sets the localized labels for attribution fields and controls.</summary>
    [Parameter]
    [EditorRequired]
    public PhotoCreditLabels Labels { get; set; } = null!;

    /// <summary>Gets or sets whether to show a compact credit with access to the full details.</summary>
    [Parameter]
    public bool Compact { get; set; }

    private PublicContentCredit Credit => Item.Credit;

    private string? TermsLabel => SuppliedTermsLabel(Item);

    private WebUrl? TermsHref => TermsAddress(Item);

    private bool HasTerms => HasDisplayTerms(Item);

    private bool HasCreditProperties => HasProperties(Item);

    private string? CreatorSummary
    {
        get
        {
            var creators = Credit.Parties.Where(party => party.Role == ContentCreditRole.Creator).Select(party => party.Name);
            var names = string.Join(", ", creators);
            return names.Length == 0 ? Credit.CreditLine : names;
        }
    }

    /// <summary>Returns whether compact rendering can show approved credit, terms, or an address for the full credit.</summary>
    /// <param name="item">The photo credit to inspect.</param>
    /// <returns>Whether the compact credit has content.</returns>
    internal static bool HasCompactCredit(PhotoCreditItem item) => item.DetailsHref is not null || HasFullCredit(item);

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Item);
        ArgumentNullException.ThrowIfNull(Labels);
        Item.Require(nameof(Item));
        Labels.Require(nameof(Labels));
    }

    private static bool HasFullCredit(PhotoCreditItem item) =>
        item.Credit.Title is not null || item.Credit.CreditLine is not null || item.Credit.CopyrightNotice is not null || HasProperties(item);

    private static bool HasProperties(PhotoCreditItem item) =>
        item.Credit.Parties.Count > 0 || item.Credit.OriginalSourceUrl is not null || HasDisplayTerms(item)
        || item.Credit.ModificationNotice is not null || item.Credit.PreviousModificationNotices.Count > 0;

    private static bool HasDisplayTerms(PhotoCreditItem item) =>
        SuppliedTermsLabel(item) is not null || TermsAddress(item) is not null || item.Credit.License.DeclaredTerms is not null;

    private static string? SuppliedTermsLabel(PhotoCreditItem item) =>
        string.IsNullOrWhiteSpace(item.TermsLabel) ? null : item.TermsLabel;

    private static WebUrl? TermsAddress(PhotoCreditItem item) =>
        item.Credit.License.Kind == ContentLicenseKind.Unknown ? null : item.Credit.License.TermsUrl;

    private string RoleLabel(ContentCreditRole role) => role switch
    {
        ContentCreditRole.Creator => Labels.Creator,
        ContentCreditRole.CopyrightOwner => Labels.CopyrightOwner,
        ContentCreditRole.Licensor => Labels.Licensor,
        _ => Labels.Designated,
    };
}
