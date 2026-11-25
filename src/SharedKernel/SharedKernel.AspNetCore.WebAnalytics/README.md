# SharedKernel.AspNetCore.WebAnalytics

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Server-side, cookieless page-view analytics for ASP.NET Core. The server records each page it answers and sends it to every configured
target: a self-hosted [Umami](https://umami.is) is built in, and any other service is a class that implements `IPageViewTarget`. The
page gets no script, no cookie and no client-side storage, so a site without scripts stays without them and needs no consent banner for
the analytics. The Aspire resource for Umami is in
[`SharedKernel.Aspire.Hosting.Umami`](../SharedKernel.Aspire.Hosting.Umami/README.md).

## Usage

```csharp
using SharedKernel.AspNetCore.WebAnalytics;

builder.AddPageViews(options => options.Hostname = "example.com")
    .AddUmami();

var app = builder.Build();
app.UseForwardedHeaders();
app.UsePageViews();
```

- `builder.AddPageViews(configure)` registers the settings every target shares (`PageViewOptions`, checked when the host starts) and
  returns a `PageViewBuilder` that adds the targets.
- `.AddUmami(connectionName = "umami", configure)` adds Umami when `ConnectionStrings:{connectionName}` is present, in the form
  `Endpoint=<Umami's base address>;WebsiteId=<the website id>`. An Aspire AppHost sets it with
  `WithReference(website, connectionName: "umami")` on the Umami website resource. The target's name, in logs and metrics, is the
  connection name.
- `UsePageViews()` goes after `UseForwardedHeaders` (so the address is the visitor's, not your proxy's) and before middleware that
  can answer without the rest of the pipeline, such as output caching (so cached pages are counted).

Nothing is read with the reflection-based configuration binder and the Umami body is serialized by a source-generated context, so the
package is trimming- and NativeAOT-safe.

### A target only when its setting is there

Page views are configured like telemetry in the service defaults, which add the OTLP exporter only when `OTEL_EXPORTER_OTLP_ENDPOINT`
is set: each target adds itself only when its own setting is present (Umami: `ConnectionStrings:{name}`), and page views are captured
only when at least one target exists. Without a target, `UsePageViews` adds nothing to the pipeline, no queue or background service
exists, and the site logs once, at information level, that page views are off (event `4205`, `PageViewsOff`). A blank connection string counts as
absent. A connection string that is present but not of the form above stops the host at start with an `OptionsValidationException` that
names the key and never repeats the value.

| `PageViewOptions` | Default | Meaning |
| --- | --- | --- |
| `Hostname` | the request host | The host name of every page view. Set it to the canonical host unless `AllowedHosts` filters the `Host` header. |
| `HonorPrivacySignals` | `true` | Leave out requests with `Sec-GPC: 1` or `DNT: 1`. |
| `QueueCapacity` | 1000 | Page views waiting for each target, 1 to 100 000. A full queue drops and counts for that target. |
| `ShutdownTimeout` | 5 seconds | How long stopping the host keeps sending what is queued, at most one minute. |

| `UmamiPageViewTargetOptions` | Default | Meaning |
| --- | --- | --- |
| `ClientIpHeader` | `X-Forwarded-For` | The header that carries the visitor's shortened address to Umami (see below). |

## What counts as a page view

A request is recorded only when all of these hold, checked after the page answered:

- at least one target is configured;
- the method is `GET` (never `HEAD`, `POST` or any other);
- the answer is `200` (never a redirect, `304`, `404`, an error page, or any other status) with an HTML content type (never a
  stylesheet, script, image, font, sitemap, `robots.txt`, health probe or API answer);
- it is not a prefetch or prerender: no `Sec-Purpose` or `Purpose` header naming `prefetch`;
- the visitor did not send `Sec-GPC: 1` or `DNT: 1` (unless `HonorPrivacySignals` is off);
- the request has a `User-Agent`, which Umami needs to register it and to drop bots.

## Privacy

A `PageView`, which every target receives, carries only:

| Field | Value |
| --- | --- |
| `PathAndQuery` | the path, with only the `utm_*` query values (`utm_source`, `utm_medium`, `utm_campaign`, `utm_content`, `utm_term`); every other query value is dropped |
| `Referrer` | the referring page on another site as scheme, host, port and path; no query, fragment or user info; none when it is the same site (the request host or `Hostname`, ignoring `www.`) or not an `http`/`https` address |
| `Language` | the first tag of `Accept-Language`, such as `pt-BR`; none when it is `*` or malformed |
| `Hostname` | `Hostname`, or the request host without its port |
| `UserAgent` | the visitor's, so a target can derive browser, OS and device and drop bots |
| `ClientIp` | the visitor's address shortened to its network (see below), so a target can derive the country and an anonymous visitor hash |

The address is shortened when the request becomes a `PageView`, before it is queued, so no queue and no target ever holds the full
address. There is no setting that turns it off.

| Address | Kept | Example |
| --- | --- | --- |
| IPv4 | the first 24 bits; the last octet becomes `0` | `203.0.113.7` becomes `203.0.113.0` |
| IPv6 | the first 48 bits; the rest becomes `0`, and no scope is kept | `2001:db8:85a3:8d3::7` becomes `2001:db8:85a3::` |
| IPv4-mapped IPv6 | shortened as the IPv4 address it maps | `::ffff:198.51.100.9` becomes `198.51.100.0` |
| none | none | |

The reason is data minimisation (GDPR art. 5(1)(c)): a target should get no more of the address than it needs. Umami 3.4 uses the
address only while it handles the page view, for the session id (a hash of the website, the address, the `User-Agent` and a monthly
salt) and to look up the country, region and city, and it stores no address. A shortened address keeps the country statistics and
roughly right visitor counts: visitors of the same network with the same browser count as one visitor in a month, and the region and
city are only as precise as the network.

Umami receives `url`, `referrer`, `language`, `hostname` and the `website` id from the connection string in the body, the `User-Agent`
header, and the shortened address in `ClientIpHeader`. What is never sent or kept: the full address, other query values (search terms,
e-mail addresses, tokens, click ids such as `gclid` or `fbclid`), the referrer's query and fragment, cookies, request bodies, the page
title, the screen size, and any identifier of the visitor. Nothing is written to the browser. `PageView` has no `ToString` that prints
the address or the `User-Agent`, and neither is ever logged: log entries carry a target name, a status code, a full exception type name or
a count, and the Umami `HttpClient` redacts every logged header value. Its own request log entries name only the Umami address.

Even shortened, the address together with the `User-Agent` is personal data in transit, so **the site's privacy notice must name the
self-hosted analytics**: what is collected (the table above, with the address shortened), the purpose (aggregate visit statistics),
that it runs on the operator's own server with no third party, that no cookie is set, that `Sec-GPC` and `DNT` are honoured, and how
long Umami keeps the data. Every other target needs the same review.

## The visitor's address

Umami 3.4 takes the address from the header named by its `CLIENT_IP_HEADER` variable when the request has it, and otherwise from the
first present of `true-client-ip`, `cf-connecting-ip`, `x-real-ip`, ..., `x-forwarded-for` (leftmost entry). Without that variable a
reverse proxy in front of Umami that sets `X-Real-IP` would make every visitor look like your web server. The
[Aspire resource](../SharedKernel.Aspire.Hosting.Umami/README.md) sets `CLIENT_IP_HEADER=X-Forwarded-For`, the target's default.
Elsewhere, set it yourself and make any proxy between the site and Umami append to `X-Forwarded-For`, never replace it. Umami 3.5
adds `TRUSTED_PROXIES`, which makes it read `X-Forwarded-For` from the right and skip the listed proxies: if you set it, list every
proxy between the site and Umami, otherwise Umami takes the last proxy's address instead of the visitor's.

## Delivery

`UsePageViews` never waits for a target. Each target has its own bounded queue and its own background loop, so a slow or broken target
delays or loses only its own page views. A page view is put on every queue without blocking, and each loop sends its queue in order,
one page view at a time. Umami gets one `POST api/send` per page view, because the visitor's `User-Agent` and address travel as request
headers (a batch shares one set of headers), through the named client `UmamiPageViewBuilderExtensions.HttpClientName`, so the host's
HTTP client defaults apply; with `PandaBear.SharedKernel.ServiceDefaults` that is the standard resilience handler, which never retries
a `POST`, so a page view is not counted twice. Nothing a target does can fail or change a page.

A page view is dropped for one target, counted on the counter `sharedkernel.web_analytics.page_views.dropped` of the meter
`SharedKernel.AspNetCore.WebAnalytics` (registered by `AddInstrumentation`) with the tags `sharedkernel.web_analytics.target` (the
target's name) and `sharedkernel.web_analytics.reason`, when:

| Reason value | When | Logged |
| --- | --- | --- |
| `queue_full` | the target's queue is full | no, to keep a slow target from flooding the log |
| `rejected` | the target answered other than 2xx | warning `4201` `PageViewSendRejected` with the target and the status code |
| `send_failed` | the send failed or timed out | warning `4202` `PageViewSendFailed` with the target and the full exception type name (`ErrorType`) |
| `shutdown` | it came after the host began stopping, or was still queued or in flight when `ShutdownTimeout` ended | warning `4204` `PageViewsUnsentAtShutdown` with the count and the target |

`WebAnalyticsTelemetry` names the meter, counter, tags, reasons and event ids. A meter listener that throws never reaches the request.
Two targets with the same name stop the host at start.

Not counted on purpose: `304` revalidations of a cached page and pages re-executed by the exception handler or status-code pages.

## Adding another target

Implement `IPageViewTarget` and add it from an extension that reads the target's own setting, so the target exists only when that
setting is present:

```csharp
public sealed class MyAnalyticsTarget(IHttpClientFactory clients) : IPageViewTarget
{
    public string Name => "my-analytics";

    public async Task Send(PageView pageView, CancellationToken ct)
    {
        using var client = clients.CreateClient("my-analytics");
        using var response = await client.PostAsync(/* the service's endpoint and body */, ct);
        response.EnsureSuccessStatusCode();
    }
}

public static PageViewBuilder AddMyAnalytics(this PageViewBuilder builder) =>
    string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("my-analytics"))
        ? builder
        : builder.AddTarget<MyAnalyticsTarget>();
```

`Send` is called from the target's own loop, one page view at a time. Report a failure by throwing: an `HttpRequestException` with a
status code (what `EnsureSuccessStatusCode` throws) counts as `rejected`, anything else as `send_failed`; the message is never logged.
`AddTarget(services => ...)` takes a factory when the target needs its settings at creation. Never log a `PageView`.

Before choosing another service, weigh what it costs:

- **Google Analytics 4** has a server-side
  [Measurement Protocol](https://developers.google.com/analytics/devguides/collection/protocol/ga4), but Google states that its intent
  "is to augment automatic collection through gtag, Tag Manager, and Google Analytics for Firebase, not to replace it" (checked
  2026-11-15). Every event needs a `client_id` (or `app_instance_id`) that identifies the browser, which a script or a cookie normally
  provides, so a GA4 target brings back the cookie and consent question this package avoids, and its reports join events through that
  id.
- **History does not move between products** through these collection APIs: they record new page views, they do not import old ones.
  The targets fan out, so during a switch the old and the new target can run side by side until the new one has enough history.
- **Self-hosted Umami keeps everything in its PostgreSQL database**, so its data is exported by querying that database (the
  `website_event` table). Umami's CSV export is a feature of Umami Cloud
  ([Export data](https://docs.umami.is/docs/cloud/export-data)), not of the self-hosted server.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.AspNetCore.WebAnalytics`. C# namespace: `SharedKernel.AspNetCore.WebAnalytics`. Assembly/project
identity: `SharedKernel.AspNetCore.WebAnalytics`. It depends on the ASP.NET Core shared framework and `PandaBear.SharedKernel.AspNetCore`
only.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private
feed. This README does not imply availability on nuget.org.
