using Microsoft.AspNetCore.Http;

namespace PandaBear.AspNetCore.WebAnalytics;

/// <summary>
/// Supplies the Umami website id that a page view is recorded under. The application implements it, for example from settings the site
/// owner edits, so the id can change without a restart; <see langword="null"/> means the page view is not recorded.
/// </summary>
/// <remarks>
/// It is resolved from the request services (registered as scoped) and called once per page view, after the page has been answered and
/// only when the request is a page view, so a site that records nothing does not pay for the lookup. Keep it cheap, for example by
/// reading a cached settings snapshot, because the request is not complete until it returns. An exception is logged as a warning with its
/// type name only and the page view is not recorded; the page is not affected. Do not keep the <see cref="HttpContext"/>.
/// </remarks>
public interface IUmamiWebsiteIdProvider
{
    /// <summary>Gets the website id for the page answered in <paramref name="context"/>.</summary>
    /// <param name="context">The request of the page view.</param>
    /// <param name="ct">The token that is cancelled when the visitor aborts the request.</param>
    /// <returns>The website id, or <see langword="null"/> to record nothing.</returns>
    ValueTask<Guid?> GetWebsiteId(HttpContext context, CancellationToken ct);
}
