using System.Globalization;
using Microsoft.AspNetCore.Components;
using SharedKernel.BuildingBlocks;

namespace SharedKernel.Blazor.Site;

/// <summary>
/// A pager for a list paged through the URL, after the GOV.UK Design System pagination component: a named <c>nav</c> landmark of plain
/// links to the previous page, the numbered pages and the next page.
/// </summary>
/// <remarks>
/// Every control is an <c>a href</c>, so crawlers follow it, it works in static server rendering without script, and an interactive
/// page renders the same markup and navigates by Blazor's link handling. Each address comes from <see cref="PageHref"/>, which decides
/// the URL of every page; give page 1 the address without a page parameter, so the first page has one canonical URL. The current page
/// carries <c>aria-current="page"</c>. The numbered links are bounded: the first page, the last page, and <see cref="SurroundingPages"/>
/// on each side of the current page; a larger gap is an ellipsis list item hidden from assistive technology, never a link, and a gap
/// of one page shows that page. There is no previous link on the first page and no next link on the last one, and nothing is rendered
/// when the list has one page. All text is caller-supplied.
/// </remarks>
public sealed partial class Pager
{
    /// <summary>The largest value of <see cref="SurroundingPages"/>.</summary>
    public const int MaxSurroundingPages = 10;

    /// <summary>Gets or sets the page window of the list; its <see cref="PageWindow.Page"/> is the current page.</summary>
    [Parameter]
    [EditorRequired]
    public PageWindow Window { get; set; }

    /// <summary>Gets or sets the address of each page from its number, for example the path alone for page 1 and <c>?page=n</c> for the others.</summary>
    [Parameter]
    [EditorRequired]
    public Func<int, LinkTarget>? PageHref { get; set; }

    /// <summary>Gets or sets the accessible name of the navigation, for example "Photo pages". Caller-supplied so it can be localized.</summary>
    [Parameter]
    [EditorRequired]
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the text of the link to the previous page, for example "Previous". Caller-supplied so it can be localized.</summary>
    [Parameter]
    [EditorRequired]
    public string PreviousText { get; set; } = string.Empty;

    /// <summary>Gets or sets the text of the link to the next page, for example "Next". Caller-supplied so it can be localized.</summary>
    [Parameter]
    [EditorRequired]
    public string NextText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional composite format for the accessible name of each numbered link, for example "Page {0}"; <c>{0}</c> is
    /// the page number. <see langword="null"/> leaves the number as the link's name.
    /// </summary>
    [Parameter]
    public string? PageLabelFormat { get; set; }

    /// <summary>Gets or sets how many pages are linked on each side of the current page, from 0 to <see cref="MaxSurroundingPages"/>. The default is 1.</summary>
    [Parameter]
    public int SurroundingPages { get; set; } = 1;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(PageHref);
        ArgumentException.ThrowIfNullOrWhiteSpace(Label);
        ArgumentException.ThrowIfNullOrWhiteSpace(PreviousText);
        ArgumentException.ThrowIfNullOrWhiteSpace(NextText);
        ArgumentOutOfRangeException.ThrowIfNegative(SurroundingPages);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(SurroundingPages, MaxSurroundingPages);
    }

    private string HrefOf(int page) => PageHref!(page).Value;

    private static string PageText(int page) => page.ToString("D", CultureInfo.CurrentCulture);

    private string? PageLabel(int page) =>
        PageLabelFormat is null ? null : string.Format(CultureInfo.CurrentCulture, PageLabelFormat, PageText(page));

    /// <summary>Lists the numbered pages to link, in order, with <see langword="null"/> for a gap of more than one page.</summary>
    private static List<int?> PageNumbers(int current, int pageCount, int surrounding)
    {
        var first = Math.Max(1, current - surrounding);
        var last = (int)Math.Min(pageCount, (long)current + surrounding);
        var shown = new SortedSet<int> { 1, pageCount };
        for (var offset = 0; offset <= last - first; offset++)
        {
            shown.Add(first + offset);
        }

        var pages = new List<int?>(shown.Count + 2);
        var previous = 0;
        foreach (var page in shown)
        {
            if (previous > 0 && page - previous == 2)
            {
                pages.Add(previous + 1);
            }
            else if (previous > 0 && page - previous > 2)
            {
                pages.Add(null);
            }

            pages.Add(page);
            previous = page;
        }

        return pages;
    }
}
