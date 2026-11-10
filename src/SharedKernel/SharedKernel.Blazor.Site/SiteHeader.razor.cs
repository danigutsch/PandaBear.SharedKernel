using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// The page banner: a brand slot, a labelled navigation and an actions slot. A navigation item with children is a native
/// <c>details</c> disclosure, so no script is needed. The item for <see cref="CurrentPath" /> gets <c>aria-current="page"</c>.
/// </summary>
public sealed partial class SiteHeader
{
    /// <summary>Gets or sets the accessible name of the navigation, for example "Main". Caller-supplied so it can be localized.</summary>
    [Parameter]
    [EditorRequired]
    public string Label
    {
        get; set;
    } = string.Empty;

    /// <summary>Gets or sets the brand, usually a link to the home page.</summary>
    [Parameter]
    public RenderFragment? Brand
    {
        get; set;
    }

    /// <summary>Gets or sets the navigation items.</summary>
    [Parameter]
    public IReadOnlyList<SiteNavItem> Items
    {
        get; set;
    } = [];

    /// <summary>Gets or sets the path of the current page, which selects the item marked as current.</summary>
    [Parameter]
    public string? CurrentPath
    {
        get; set;
    }

    /// <summary>Gets or sets content after the navigation, for example a language switcher.</summary>
    [Parameter]
    public RenderFragment? Actions
    {
        get; set;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Items);
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

    private string? Current(SiteNavItem item) => IsCurrent(item) ? "page" : null;

    private bool IsCurrent(SiteNavItem item) => CurrentPath is not null && item.Href is not null
        && string.Equals(Normalize(item.Href.Value), Normalize(CurrentPath), StringComparison.OrdinalIgnoreCase);

    private bool HasCurrentChild(SiteNavItem item) => item.Children?.Any(IsCurrent) == true;

    private static string Normalize(string path)
    {
        var end = path.IndexOfAny(['?', '#']);
        var trimmed = (end < 0 ? path : path[..end]).TrimEnd('/');
        return trimmed.Length == 0 ? "/" : trimmed;
    }
}
