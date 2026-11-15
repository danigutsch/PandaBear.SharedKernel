using System.Globalization;
using System.Net.Http.Headers;

namespace PandaBear.Forgejo;

/// <summary>
/// Reads every page of a Forgejo list. <see href="https://forgejo.org/docs/latest/user/api-usage/">API usage</see>
/// documents <c>page</c> and <c>limit</c>, a <c>Link</c> header with the next, previous, and last pages when there is more
/// than one page, and <c>X-Total-Count</c>; the instance caps <c>limit</c> at its <c>max_response_items</c>, 50 by
/// default. Not every list sends a <c>Link</c> header: Forgejo 17 answers the labels of a repository with
/// <c>X-Total-Count</c> only.
/// </summary>
public static class ForgejoPager
{
    /// <summary>The <c>limit</c> that callers request: the default <c>max_response_items</c>.</summary>
    public const int PageSize = 50;

    /// <summary>
    /// Reads pages 1, 2, and so on through <paramref name="readPage"/> and returns their items in order. After each page
    /// it stops when the page is empty; otherwise, when the page has a <c>Link</c> header, when that header has no
    /// <c>next</c> relation; otherwise, when it has an <c>X-Total-Count</c>, when that many items have been read;
    /// otherwise, when the page holds fewer than <see cref="PageSize"/> items.
    /// </summary>
    /// <typeparam name="T">The type of the items.</typeparam>
    /// <param name="readPage">Reads the page with the given 1-based number, requested with <c>limit=</c><see cref="PageSize"/>.</param>
    /// <param name="maxPages">The most pages to read; a longer list fails instead of being returned in part.</param>
    /// <param name="description">What the list is, such as <c>The releases of owner/name</c>, for the failure message.</param>
    /// <param name="cancellationToken">The token that cancels the reading.</param>
    /// <returns>Every item of the list.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="readPage"/> or <paramref name="description"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxPages"/> is less than 1.</exception>
    /// <exception cref="InvalidOperationException">The list has more than <paramref name="maxPages"/> pages.</exception>
    public static async Task<IReadOnlyList<T>> ReadAll<T>(
        Func<int, CancellationToken, Task<ForgejoPage<T>>> readPage,
        int maxPages,
        string description,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(readPage);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPages, 1);

        List<T> items = [];
        for (var number = 1; number <= maxPages; number++)
        {
            var page = await readPage(number, cancellationToken).ConfigureAwait(false);
            items.AddRange(page.Items);
            if (IsLast(page, items.Count))
            {
                return items;
            }
        }

        throw new InvalidOperationException(string.Create(
            CultureInfo.InvariantCulture,
            $"{description} span more than {maxPages} pages of {PageSize} items; nothing was read from a partial list."));
    }

    /// <summary>Returns the page of <paramref name="items"/> with the <c>Link</c> and <c>X-Total-Count</c> headers of a response.</summary>
    /// <typeparam name="T">The type of the items.</typeparam>
    /// <param name="items">The items read from the response body.</param>
    /// <param name="headers">The headers of the response.</param>
    /// <returns>The page; a missing, negative, or unreadable total count is <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> or <paramref name="headers"/> is <see langword="null"/>.</exception>
    public static ForgejoPage<T> Page<T>(IReadOnlyList<T> items, HttpResponseHeaders headers)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(headers);
        var link = headers.TryGetValues("Link", out var links) ? string.Join(',', links) : null;
        long? total = headers.TryGetValues("X-Total-Count", out var totals)
            && long.TryParse(totals.FirstOrDefault(), NumberStyles.None, CultureInfo.InvariantCulture, out var count)
                ? count
                : null;
        return new ForgejoPage<T>(items, link, total);
    }

    private static bool IsLast<T>(ForgejoPage<T> page, int read)
    {
        if (page.Items.Count == 0)
        {
            return true;
        }

        if (page.Link is not null)
        {
            return !page.Link.Split(',').Any(static part => part.Contains("rel=\"next\"", StringComparison.Ordinal));
        }

        if (page.TotalCount is { } total)
        {
            return read >= total;
        }

        return page.Items.Count < PageSize;
    }
}
