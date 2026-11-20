using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// The page banner: a brand slot, a labelled navigation and an actions slot. A navigation item with children is a native
/// <c>details</c> disclosure, so no script is needed; it starts closed. The item for <see cref="CurrentPath" /> gets
/// <c>aria-current="page"</c>, and the summary of the group that holds it gets <c>aria-current="true"</c>. With
/// <see cref="MenuLabel" /> the navigation and the actions are the content of one more <c>details</c>, whose summary is the menu
/// button below the breakpoint; from the breakpoint on, a browser that supports <c>::details-content</c> shows them inline without
/// the button, and an older one keeps the button. <see cref="Placement" /> lays the header over a cover hero.
/// </summary>
/// <remarks>
/// Matching compares paths only: the query and fragment are ignored, a missing leading slash is added, a trailing slash is
/// ignored, and the comparison is ordinal and ignores case. So <c>tours</c>, <c>/tours/</c> and <c>/Tours?page=2</c> all match an
/// item whose address is <c>/tours</c>, and the empty path matches <c>/</c>. An absolute item address never matches.
/// </remarks>
public sealed partial class SiteHeader
{
    /// <summary>
    /// Gets or sets the accessible name of the navigation, for example "Main". Caller-supplied so it can be localized. Required when
    /// <see cref="Items" /> is not empty.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the brand, usually a link to the home page.</summary>
    [Parameter]
    public RenderFragment? Brand { get; set; }

    /// <summary>Gets or sets the navigation items.</summary>
    [Parameter]
    public IReadOnlyList<SiteNavItem> Items { get; set; } = [];

    /// <summary>
    /// Gets or sets the path of the current page, which selects the item marked as current, for example
    /// <c>Navigation.ToBaseRelativePath(Navigation.Uri)</c> or <c>/tours</c>.
    /// </summary>
    [Parameter]
    public string? CurrentPath { get; set; }

    /// <summary>Gets or sets where the header lies. The default is <see cref="HeaderPlacement.Above" />.</summary>
    [Parameter]
    public HeaderPlacement Placement { get; set; }

    /// <summary>
    /// Gets or sets the label of the menu button, for example "Menu". Caller-supplied so it can be localized. When set, the navigation
    /// and the actions collapse behind that button below the <c>48rem</c> breakpoint; from it on they show inline and the button is
    /// hidden. Null or blank keeps them always shown.
    /// </summary>
    [Parameter]
    public string? MenuLabel { get; set; }

    /// <summary>Gets or sets content after the navigation, for example a language switcher.</summary>
    [Parameter]
    public RenderFragment? Actions { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Items);
        if (!Enum.IsDefined(Placement))
        {
            throw new ArgumentOutOfRangeException(nameof(Placement), Placement, "The header placement is not defined.");
        }

        if (Items.Count > 0)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(Label);
        }

        foreach (var item in Items)
        {
            Validate(item, allowChildren: true);
            foreach (var child in item.Children ?? [])
            {
                Validate(child, allowChildren: false);
            }
        }
    }

#pragma warning disable CA2208 // The invalid entry belongs to the Items parameter of the component.
    private static void Validate(SiteNavItem item, bool allowChildren)
    {
        ArgumentNullException.ThrowIfNull(item, nameof(Items));
        ArgumentException.ThrowIfNullOrWhiteSpace(item.Text, nameof(Items));
        var hasChildren = item.Children is { Count: > 0 };
        if (hasChildren && (!allowChildren || item.Href is not null))
        {
            throw new ArgumentException("A navigation group has children and no Href, and children cannot have children.", nameof(Items));
        }

        if (!hasChildren && item.Href is null)
        {
            throw new ArgumentException("A navigation item needs an Href or children.", nameof(Items));
        }
    }
#pragma warning restore CA2208

    private bool HasMenu => !string.IsNullOrWhiteSpace(MenuLabel) && (Items.Count > 0 || Actions is not null);

    private string? Current(SiteNavItem item) => IsCurrent(item) ? "page" : null;

    private bool IsCurrent(SiteNavItem item) => CurrentPath is not null && item.Href is not null
        && string.Equals(Normalize(item.Href.Value), Normalize(CurrentPath), StringComparison.OrdinalIgnoreCase);

    private string? CurrentGroup(SiteNavItem item) => item.Children?.Any(IsCurrent) == true ? "true" : null;

    private static string Normalize(string path)
    {
        var end = path.IndexOfAny(['?', '#']);
        var trimmed = (end < 0 ? path : path[..end]).Trim('/');
        return "/" + trimmed;
    }
}
