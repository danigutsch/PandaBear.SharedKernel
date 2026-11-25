using System.Net;
using System.Net.Mime;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace SharedKernel.AspNetCore.WebAnalytics;

/// <summary>Decides from an answered request whether it is a page view, and keeps of it only what the targets need.</summary>
internal static class PageViewCapture
{
    private const string SecPurposeHeader = "Sec-Purpose";
    private const string PurposeHeader = "Purpose";
    private const string GlobalPrivacyControlHeader = "Sec-GPC";
    private const string DoNotTrackHeader = "DNT";
    private const string PrefetchPurpose = "prefetch";
    private const string SignalSet = "1";
    private const string CampaignKeyPrefix = "utm_";
    private const string WorldWideWebPrefix = "www.";
    private const int MaximumLanguageSubtagLength = 8;
    private const int IPv4NetworkBytes = 3;
    private const int IPv6NetworkBytes = 6;

    private static readonly char[] ListSeparators = [',', ';'];

    /// <summary>
    /// Returns whether the answered request is a page view: a GET answered 200 with an HTML page and a <c>User-Agent</c>, that is not a
    /// prefetch or prerender (<c>Sec-Purpose</c> or <c>Purpose</c> naming <c>prefetch</c>), and, when <paramref name="honorPrivacySignals"/>
    /// is set, whose visitor did not send <c>Sec-GPC: 1</c> or <c>DNT: 1</c>.
    /// </summary>
    /// <param name="context">The request after the page answered it.</param>
    /// <param name="honorPrivacySignals">Whether <c>Sec-GPC</c> and <c>DNT</c> stop the page view.</param>
    /// <returns><see langword="true"/> when the request is a page view.</returns>
    public static bool IsPageView(HttpContext context, bool honorPrivacySignals)
    {
        var request = context.Request;
        var response = context.Response;
        return HttpMethods.IsGet(request.Method)
            && response.StatusCode == StatusCodes.Status200OK
            && IsHtml(response.ContentType)
            && !IsPrefetch(request.Headers)
            && !(honorPrivacySignals && AsksNotToBeTracked(request.Headers))
            && !StringValues.IsNullOrEmpty(request.Headers.UserAgent);
    }

    /// <summary>
    /// Keeps of a page view only what the targets need: no query value except <c>utm_*</c>, no referrer query or fragment, no same-site
    /// referrer, and the visitor's address shortened to its network (see <see cref="PageView.ClientIp"/>).
    /// </summary>
    /// <param name="context">The request of the page view.</param>
    /// <param name="configuredHostname">The configured host name, or <see langword="null"/> to use the request host.</param>
    /// <returns>The page view.</returns>
    public static PageView Capture(HttpContext context, string? configuredHostname)
    {
        var request = context.Request;
        var requestHost = request.Host.HasValue ? request.Host.Host : null;
        return new PageView
        {
            Hostname = configuredHostname ?? requestHost,
            PathAndQuery = PathAndQuery(request),
            Referrer = Referrer(request.Headers.Referer, requestHost, configuredHostname),
            Language = Language(request.Headers.AcceptLanguage),
            UserAgent = request.Headers.UserAgent.ToString(),
            ClientIp = Shorten(context.Connection.RemoteIpAddress),
        };
    }

    private static bool IsHtml(string? contentType) =>
        MediaTypeHeaderValue.TryParse(contentType, out var mediaType)
        && mediaType.MediaType.Equals(MediaTypeNames.Text.Html, StringComparison.OrdinalIgnoreCase);

    private static bool IsPrefetch(IHeaderDictionary headers) =>
        NamesPrefetch(headers[SecPurposeHeader]) || NamesPrefetch(headers[PurposeHeader]);

    private static bool NamesPrefetch(StringValues values)
    {
        foreach (var value in values)
        {
            foreach (var token in (value ?? string.Empty).Split(ListSeparators, StringSplitOptions.TrimEntries))
            {
                if (token.Equals(PrefetchPurpose, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool AsksNotToBeTracked(IHeaderDictionary headers) =>
        IsSet(headers[GlobalPrivacyControlHeader]) || IsSet(headers[DoNotTrackHeader]);

    private static bool IsSet(StringValues values) =>
        values.Count > 0 && string.Equals(values[0]?.Trim(), SignalSet, StringComparison.Ordinal);

    private static string PathAndQuery(HttpRequest request)
    {
        var path = (request.PathBase + request.Path).ToUriComponent();
        var campaign = request.Query
            .Where(static pair => pair.Key.StartsWith(CampaignKeyPrefix, StringComparison.Ordinal))
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .ToArray();
        return campaign.Length == 0 ? path : path + QueryString.Create(campaign).ToUriComponent();
    }

    private static string? Referrer(StringValues referer, string? requestHost, string? configuredHostname)
    {
        if (!Uri.TryCreate(referer.ToString(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || IsSameSite(uri.Host, requestHost)
            || IsSameSite(uri.Host, configuredHostname))
        {
            return null;
        }

        return uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped);
    }

    private static bool IsSameSite(string referrerHost, string? siteHost) =>
        siteHost is not null && string.Equals(WithoutWorldWideWeb(referrerHost), WithoutWorldWideWeb(siteHost), StringComparison.OrdinalIgnoreCase);

    private static string WithoutWorldWideWeb(string host) =>
        host.StartsWith(WorldWideWebPrefix, StringComparison.OrdinalIgnoreCase) ? host[WorldWideWebPrefix.Length..] : host;

    private static string? Language(StringValues acceptLanguage)
    {
        var header = acceptLanguage.ToString();
        var end = header.IndexOfAny(ListSeparators);
        var tag = (end < 0 ? header : header[..end]).Trim();
        return IsLanguageTag(tag) ? tag : null;
    }

    private static bool IsLanguageTag(string tag)
    {
        var subtags = tag.Split('-');
        return tag.Length > 0
            && subtags[0].All(char.IsAsciiLetter)
            && subtags.All(static subtag => subtag.Length is > 0 and <= MaximumLanguageSubtagLength && subtag.All(char.IsAsciiLetterOrDigit));
    }

    private static IPAddress? Shorten(IPAddress? address)
    {
        if (address is null)
        {
            return null;
        }

        var visitor = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        var bytes = visitor.GetAddressBytes();
        var networkBytes = visitor.AddressFamily == AddressFamily.InterNetwork ? IPv4NetworkBytes : IPv6NetworkBytes;
        bytes.AsSpan(networkBytes).Clear();
        return new IPAddress(bytes);
    }
}
