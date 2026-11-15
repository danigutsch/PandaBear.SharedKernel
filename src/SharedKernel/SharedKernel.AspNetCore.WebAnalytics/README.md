# SharedKernel.AspNetCore.WebAnalytics

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Server-side, cookieless page-view analytics for ASP.NET Core. The server records each page it answers and sends it to a
self-hosted [Umami](https://umami.is) through Umami's documented `POST /api/send`. The page gets no script, no cookie and no
client-side storage, so a site without scripts stays without them and needs no consent banner for the analytics. The Aspire
resource for Umami is in [`SharedKernel.Aspire.Hosting.Umami`](../SharedKernel.Aspire.Hosting.Umami/README.md).

## Usage

```csharp
using PandaBear.AspNetCore.WebAnalytics;

builder.Services.AddUmamiPageViews<SettingsWebsiteIdProvider>(
    "ConnectionStrings:umami",
    options => options.Hostname = "example.com");

var app = builder.Build();
app.UseForwardedHeaders();
app.UseUmamiPageViews();
```

```csharp
public sealed class SettingsWebsiteIdProvider(ISiteSettingsReader settings) : IUmamiWebsiteIdProvider
{
    public async ValueTask<Guid?> GetWebsiteId(HttpContext context, CancellationToken ct) =>
        (await settings.Current(ct)).UmamiWebsiteId;
}
```

- `AddUmamiPageViews<TWebsiteIdProvider>(endpointConfigurationKey, configure)` reads the Umami base address from one configuration
  value, such as `ConnectionStrings:umami` that an Aspire AppHost sets with `WithReference(umami)`, and checks the settings when the
  host starts: a missing or invalid address stops the host with an `OptionsValidationException` naming the key. The value is read
  without the reflection-based binder and the body is serialized by a source-generated context, so the package is trimming- and
  NativeAOT-safe.
- `IUmamiWebsiteIdProvider` is the application's source of the Umami website id, registered as scoped. It can read a setting the site
  owner edits, so the id changes without a restart. `null` records nothing. It is called once per page view after the page is answered,
  so keep it cheap (a cached settings snapshot); a throwing lookup is logged with its type name and the page view is skipped.
- `UseUmamiPageViews()` goes after `UseForwardedHeaders` (so the address is the visitor's, not your proxy's) and before middleware that
  can answer without the rest of the pipeline, such as output caching (so cached pages are counted).

| `UmamiPageViewOptions` | Default | Meaning |
| --- | --- | --- |
| `Endpoint` | from the configuration key | Umami's base address; page views go to `api/send` below it, keeping its path. |
| `Hostname` | the request host | The `hostname` sent. Set it to the canonical host unless `AllowedHosts` filters the `Host` header. |
| `HonorPrivacySignals` | `true` | Leave out requests with `Sec-GPC: 1` or `DNT: 1`. |
| `ClientIpHeader` | `X-Forwarded-For` | The header that carries the visitor's address to Umami (see below). |
| `QueueCapacity` | 1000 | Page views waiting for the sender, 1 to 100 000. A full queue drops and counts. |
| `ShutdownTimeout` | 5 seconds | How long stopping the host keeps sending what is queued, at most one minute. |

## What counts as a page view

A request is recorded only when all of these hold, checked after the page answered:

- the method is `GET` (never `HEAD`, `POST` or any other);
- the answer is `200` (never a redirect, `304`, `404`, an error page, or any other status) with an HTML content type (never a
  stylesheet, script, image, font, sitemap, `robots.txt`, health probe or API answer);
- it is not a prefetch or prerender: no `Sec-Purpose` or `Purpose` header naming `prefetch`;
- the visitor did not send `Sec-GPC: 1` or `DNT: 1` (unless `HonorPrivacySignals` is off);
- the request has a `User-Agent`, which Umami needs to register it and to drop bots;
- the provider returns a website id.

## Privacy

What is sent to Umami, and only to Umami:

| Field | Value |
| --- | --- |
| `url` | the path, with only the `utm_*` query values (`utm_source`, `utm_medium`, `utm_campaign`, `utm_content`, `utm_term`); every other query value is dropped |
| `referrer` | the referring page on another site as scheme, host, port and path; no query, fragment or user info; none when it is the same site (the request host or `Hostname`, ignoring `www.`) or not an `http`/`https` address |
| `language` | the first tag of `Accept-Language`, such as `pt-BR`; none when it is `*` or malformed |
| `hostname` | `Hostname`, or the request host without its port |
| `website` | the id from `IUmamiWebsiteIdProvider` |
| `User-Agent` header | the visitor's, so Umami can derive browser, OS and device and drop bots |
| `ClientIpHeader` header | the visitor's address (IPv4-mapped addresses as IPv4, no IPv6 scope), so Umami can derive the country and the anonymous visitor hash |

What is never sent or kept: other query values (search terms, e-mail addresses, tokens, click ids such as `gclid` or `fbclid`), the
referrer's query and fragment, cookies, request bodies, the page title, the screen size, and any identifier of the visitor. Nothing is
written to the browser. The address and the `User-Agent` are never logged: log entries carry an Umami status code, an exception type
name or a count, and the named `HttpClient` redacts every logged header value. Its own request log entries name only the Umami address.

Umami turns the address and `User-Agent` into a salted visitor hash and a location and does not store the address. It is personal
data in transit all the same, so **the site's privacy notice must name the self-hosted analytics**: what is collected (the table above),
the purpose (aggregate visit statistics), that it runs on the operator's own server with no third party, that no cookie is set, that
`Sec-GPC` and `DNT` are honoured, and how long Umami keeps the data.

## The visitor's address

Umami 3.4 takes the address from the header named by its `CLIENT_IP_HEADER` variable when the request has it, and otherwise from the
first present of `true-client-ip`, `cf-connecting-ip`, `x-real-ip`, ..., `x-forwarded-for` (leftmost entry). Without that variable a
reverse proxy in front of Umami that sets `X-Real-IP` would make every visitor look like your web server. The
[Aspire resource](../SharedKernel.Aspire.Hosting.Umami/README.md) sets `CLIENT_IP_HEADER=X-Forwarded-For`, the forwarder's default.
Elsewhere, set it yourself and make any proxy between the site and Umami append to `X-Forwarded-For`, never replace it. Umami 3.5
adds `TRUSTED_PROXIES`, which makes it read `X-Forwarded-For` from the right and skip the listed proxies: if you set it, list every
proxy between the site and Umami, otherwise Umami takes the last proxy's address instead of the visitor's.

## Delivery

`UseUmamiPageViews` never waits for Umami. The page view is put on a bounded queue without blocking, and a hosted service sends the
queue in order, one `POST api/send` per page view, because the visitor's `User-Agent` and address travel as request headers (a batch
shares one set of headers). It uses the named client `UmamiPageViewServiceCollectionExtensions.HttpClientName`, so the host's HTTP client
defaults apply; with `PandaBear.SharedKernel.ServiceDefaults` that is the standard resilience handler, which never retries a `POST`,
so a page view is not counted twice. Nothing it does can fail or change a page.

A page view is dropped, counted on the counter `web_analytics_page_views_dropped_total` of the meter
`PandaBear.AspNetCore.WebAnalytics` (registered by `AddSharedKernelInstrumentation`) with the tag `reason`, when:

| `reason` | When | Logged |
| --- | --- | --- |
| `queue_full` | the queue is full | no, to keep a slow Umami from flooding the log |
| `rejected` | Umami answered other than 2xx | warning `4201` with the status code |
| `send_failed` | the request failed or timed out | warning `4202` with the exception type name |
| `shutdown` | it came after the host began stopping, or was still queued or in flight when `ShutdownTimeout` ended | warning `4204` with the count |

A failing website id lookup is warning `4203` with the exception type name. `WebAnalyticsTelemetry` names the meter, counter, tag,
reasons and event ids. A meter listener that throws never reaches the request.

Not counted on purpose: `304` revalidations of a cached page and pages re-executed by the exception handler or status-code pages.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.AspNetCore.WebAnalytics`. C# namespace: `PandaBear.AspNetCore.WebAnalytics`. Assembly/project
identity: `SharedKernel.AspNetCore.WebAnalytics`. It depends on the ASP.NET Core shared framework and `PandaBear.SharedKernel.AspNetCore`
only.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private
feed. This README does not imply availability on nuget.org.
