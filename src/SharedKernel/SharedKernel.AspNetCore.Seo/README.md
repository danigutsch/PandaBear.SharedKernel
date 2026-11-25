# SharedKernel.AspNetCore.Seo

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Crawler-facing helpers for ASP.NET Core. The consuming application owns crawler policy, sitemap URL
inclusion, and approval of photo rights metadata for publication.

## What is inside

- `MapRobotsTxt`: exposes an application-owned crawler policy at `/robots.txt` with `text/plain; charset=utf-8` for `GET` and
  `HEAD` (the same headers, including `Content-Length`, without the body).
- `SitemapXmlSerializer`: generates sitemap XML (`SitemapXmlSerializer.ContentType`, at most
  `SitemapXmlSerializer.MaximumUrlCount` URLs) from `SitemapEntry` values.
- `SitemapAlternate`: an alternate-language URL of a `SitemapEntry` (`new SitemapEntry(uri, lastModified, alternates)`),
  written as `<xhtml:link rel="alternate" hreflang="..." href="..." />`; `SitemapAlternate.ForDefault` creates the
  `x-default` entry. The `xhtml` namespace is declared only when an entry has alternates.
- `SitemapCanonicalOrigin`: validates and parses the canonical origin that sitemap locations must use.
- `PhotoLicenseJsonLd`: projects the approved public credit for a served photo as Schema.org image metadata.
- `AddCanonicalOrigin(configurationKey)`: binds `CanonicalOriginOptions` from one configuration value (such as `PublicSite:Origin`) and
  validates it on start, so a host with a missing or invalid origin does not start (`OptionsValidationException` naming the key).
  `CanonicalOriginOptions.Value` is the origin and `Absolute("/path")` builds an address on it. Build canonical, `hreflang`, OpenGraph and
  sitemap addresses from it, never from the request's `Host` header. The value is read without the reflection-based binder.

## Localized pages

Pass each published language link and its optional last-modified value to `LocalizedSitemap.Build`.
The builder emits one entry per link, in the same order, with the full set of reciprocal alternates.
Supply the fallback URI separately when the site uses `x-default`, including when that URI redirects.
Pass the resulting entries to `SitemapXmlSerializer.Serialize`.

## Photo rights on published pages

For an existing BuildingBlocks `ImageCredit`, use
[`ContentAttribution.FromImageCredit`](../SharedKernel.ContentRights/README.md#existing-image-credits)
before passing its reviewed projection to both the gallery and the serializer.

Pass the actual public image URL and the same approved `PublicContentCredit` used in the visible
photo attribution to `PhotoLicenseJsonLd.Serialize`. For a gallery, serialize each eligible image on the
current album page. Supply the absolute URL from the configured public site or approved media origin.
Leave the image URL unset for a private, draft, or otherwise unpublished image. The caller decides whether
the photo and its credit are public. The serializer works from the supplied record and URL without fetching them.

| Schema.org field | Value supplied by the application |
| --- | --- |
| `contentUrl` | The served public image URL. The original source URL belongs in the visible credit. |
| `creator` | The names of explicitly typed creators. Each entry is a `Person` or `Organization`. |
| `creditText` | The approved credit line. Use the same line in the image caption or linked credits. |
| `copyrightNotice` | The approved copyright notice supplied with the photo. |
| `license` | A recognized Creative Commons terms URL, a CC0 dedication URL, or an explicitly approved custom terms URL. |
| `acquireLicensePage` | The public page for requesting a license to this photo, when one exists. |

IPTC Creator, Credit Line and Copyright Notice correspond to the three credit fields. The IPTC Web
Statement of Rights can supply a reviewed terms URL. A Licensor URL can supply the request page
when it serves that purpose. A credit also may have a title, original source and modification notices
for the visible attribution. Keep approval evidence private and preserve upload metadata stripping.
The serializer omits `license` for unknown rights, reserved rights, individual permissions, the Public
Domain Mark and incomplete Creative Commons declarations. The visible credit can still describe them.
[IPTC Photo Metadata User Guide](https://www.iptc.org/std/photometadata/documentation/userguide/) ·
[Schema.org ImageObject](https://schema.org/ImageObject) ·
[Google image license metadata](https://developers.google.com/search/docs/appearance/structured-data/image-license-metadata)

In a Razor component, render the returned value in a JSON-LD script next to the published photo
markup or in the containing page head. The serializer's default JSON encoder escapes HTML
delimiters in values, so the resulting payload can be inserted as markup without double encoding.

```razor
@if (PhotoLicenseJsonLd.Serialize(photo.PublicUrl, photo.PublicCredit, photo.LicenseRequestPage) is { } json)
{
    <script type="application/ld+json">@((MarkupString)json)</script>
}
```

Check the rendered page with Google's Rich Results Test or the Schema.org validator. Compare the
terms and credit against the visible attribution for the same photo.
[.NET JSON character encoding](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/character-encoding)

## Install

```sh
dotnet add package PandaBear.SharedKernel.AspNetCore.Seo
```

```csharp
using SharedKernel.AspNetCore.Seo;

app.MapRobotsTxt("User-agent: *\nDisallow: /admin\n");
app.MapGet("/sitemap.xml", async (CancellationToken cancellationToken) =>
{
    var origin = SitemapCanonicalOrigin.Parse("https://example.com");
    SitemapEntry[] entries = [new(new Uri(origin, "/about"))];
    return Results.Stream(await SitemapXmlSerializer.Serialize(entries, cancellationToken), SitemapXmlSerializer.ContentType);
});
```

## Migrating from SharedKernel.AspNetCore

`AspNetCoreRobotsTxtExtensions`, `SitemapCanonicalOrigin`, `SitemapEntry` and `SitemapXmlSerializer` moved from
`PandaBear.SharedKernel.AspNetCore` (namespace `SharedKernel.AspNetCore`) into this package and namespace
`SharedKernel.AspNetCore.Seo`. Add the package reference and replace `using SharedKernel.AspNetCore;` with
`using SharedKernel.AspNetCore.Seo;` where they are used.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.AspNetCore.Seo`. C# namespace: `SharedKernel.AspNetCore.Seo`. Assembly/project
identity: `SharedKernel.AspNetCore.Seo`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured
local/private feed. This README does not imply availability on nuget.org.
