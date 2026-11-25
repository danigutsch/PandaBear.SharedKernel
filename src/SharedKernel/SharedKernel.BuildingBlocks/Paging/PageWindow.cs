namespace SharedKernel.BuildingBlocks;

/// <summary>
/// The page of a list that a request shows, computed from the page number in the URL, a fixed page size and the number of items.
/// Model the page number in the URL (for example <c>?page=3</c>) so a reload, a shared link and static rendering show the same page,
/// and read only <see cref="Skip"/> and <see cref="Take"/> items instead of the whole list.
/// </summary>
/// <remarks>
/// The requested page is clamped like the URL paging of <c>QuickGrid</c>: a missing, malformed, zero or negative number shows the
/// first page, and a number beyond the end shows the last page; <see cref="IsOutOfRange"/> tells which requests were clamped. An empty
/// list has one page. Pages are numbered from 1. This is offset paging, which numbered pages need: order the query by a unique,
/// immutable key (for example the id as the last tie-breaker), or rows move between pages from one request to the next. The default
/// value has no pages and is not a valid window; create one with <see cref="Create(int?, int, int)"/>.
/// </remarks>
public readonly record struct PageWindow
{
    private PageWindow(int page, int pageSize, int total, int pageCount, bool isOutOfRange)
    {
        Page = page;
        PageSize = pageSize;
        Total = total;
        PageCount = pageCount;
        IsOutOfRange = isOutOfRange;
    }

    /// <summary>Gets the page shown, from 1 to <see cref="PageCount"/>.</summary>
    public int Page { get; }

    /// <summary>Gets the largest number of items on a page.</summary>
    public int PageSize { get; }

    /// <summary>Gets the number of items in the list.</summary>
    public int Total { get; }

    /// <summary>Gets the number of pages, at least 1.</summary>
    public int PageCount { get; }

    /// <summary>Gets the number of items before the page.</summary>
    public int Skip => Page > 0 ? (Page - 1) * PageSize : 0;

    /// <summary>Gets the largest number of items to read for the page, <see cref="PageSize"/>.</summary>
    public int Take => PageSize;

    /// <summary>Gets the number of the previous page, or <see langword="null"/> on the first page.</summary>
    public int? PreviousPage => Page > 1 ? Page - 1 : null;

    /// <summary>Gets the number of the next page, or <see langword="null"/> on the last page.</summary>
    public int? NextPage => Page < PageCount ? Page + 1 : null;

    /// <summary>
    /// Gets whether the request asked for a page that does not exist: a number below 1 or beyond <see cref="PageCount"/>, or query text
    /// that is not a page number. <see cref="Page"/> is clamped either way; a public site can answer 404 Not Found instead, so a crawler
    /// does not index endless copies of the last page, while an admin page may show the clamped page. A request without a page is never
    /// out of range.
    /// </summary>
    public bool IsOutOfRange { get; }

    /// <summary>Gets whether the list has more than one page, so a pager is needed.</summary>
    public bool HasMultiplePages => PageCount > 1;

    /// <summary>Computes the window for a requested page number.</summary>
    /// <param name="requestedPage">The page asked for, or <see langword="null"/> for the first page; it is clamped to the pages that exist.</param>
    /// <param name="pageSize">The largest number of items on a page.</param>
    /// <param name="total">The number of items in the list.</param>
    /// <returns>The window.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageSize"/> is zero or negative, or <paramref name="total"/> is negative.</exception>
    public static PageWindow Create(int? requestedPage, int pageSize, int total) => Create(requestedPage, malformed: false, pageSize, total);

    /// <summary>Computes the window for a page number read from the query string.</summary>
    /// <param name="requestedPage">
    /// The query string value, or <see langword="null"/> when the URL has none. Only ASCII digits are a page number; anything else,
    /// including a sign, a space or an empty value, shows the first page and is <see cref="IsOutOfRange"/>. Digits beyond the range of
    /// <see cref="int"/> show the last page.
    /// </param>
    /// <param name="pageSize">The largest number of items on a page.</param>
    /// <param name="total">The number of items in the list.</param>
    /// <returns>The window.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageSize"/> is zero or negative, or <paramref name="total"/> is negative.</exception>
    public static PageWindow Create(string? requestedPage, int pageSize, int total)
    {
        if (requestedPage is null)
        {
            return Create(null, malformed: false, pageSize, total);
        }

        return ParsePage(requestedPage) is { } page
            ? Create(page, malformed: false, pageSize, total)
            : Create(null, malformed: true, pageSize, total);
    }

    private static PageWindow Create(int? requestedPage, bool malformed, int pageSize, int total)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        ArgumentOutOfRangeException.ThrowIfNegative(total);

        var pageCount = Math.Max(1, (total / pageSize) + (total % pageSize == 0 ? 0 : 1));
        var page = requestedPage is { } requested ? Math.Clamp(requested, 1, pageCount) : 1;
        var outOfRange = malformed || (requestedPage is { } asked && asked != page);
        return new PageWindow(page, pageSize, total, pageCount, outOfRange);
    }

    private static int? ParsePage(string text)
    {
        if (text.Length == 0 || !text.All(char.IsAsciiDigit))
        {
            return null;
        }

        var page = 0;
        foreach (var digit in text)
        {
            if (page > (int.MaxValue - (digit - '0')) / 10)
            {
                return int.MaxValue;
            }

            page = (page * 10) + (digit - '0');
        }

        return page;
    }
}
