# SharedKernel.AspNetCore.Seo

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Crawler-facing helpers for ASP.NET Core: a `/robots.txt` endpoint and sitemap XML generation. The consuming
application keeps the crawler policy and the URL inclusion policy.

## What is inside

- `MapRobotsTxt`: exposes an application-owned crawler policy at `/robots.txt` with `text/plain; charset=utf-8`.
- `SitemapXmlSerializer`: generates sitemap XML (`SitemapXmlSerializer.ContentType`, at most
  `SitemapXmlSerializer.MaximumUrlCount` URLs) from `SitemapEntry` values.
- `SitemapCanonicalOrigin`: validates and parses the canonical origin that sitemap locations must use.

## Install

```sh
dotnet add package PandaBear.SharedKernel.AspNetCore.Seo
```

```csharp
using PandaBear.AspNetCore.Seo;

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
`PandaBear.SharedKernel.AspNetCore` (namespace `PandaBear.AspNetCore`) into this package and namespace
`PandaBear.AspNetCore.Seo`. Add the package reference and replace `using PandaBear.AspNetCore;` with
`using PandaBear.AspNetCore.Seo;` where they are used.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.AspNetCore.Seo`. C# namespace: `PandaBear.AspNetCore.Seo`. Assembly/project
identity: `SharedKernel.AspNetCore.Seo`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured
local/private feed. This README does not imply availability on nuget.org.
