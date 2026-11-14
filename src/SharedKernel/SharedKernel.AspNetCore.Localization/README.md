# SharedKernel.AspNetCore.Localization

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Path-prefix localization for ASP.NET Core sites whose URLs start with the culture (`/pt/tours`, `/en/tours`). The consuming
application owns the list of languages, the excluded routes, and the translated content; this package supplies the
mechanics. It builds on the `SupportedCultures` and `ContentCulture` value objects of
`PandaBear.SharedKernel.BuildingBlocks`.

## What is inside

- `PathCultureRequestCultureProvider`: a `RequestCultureProvider` that reads the first path segment against a
  `SupportedCultures` set, for `UseRequestLocalization`. Like `CulturePath.TryParse`, it accepts only the exact canonical
  segment (`/en/tours`, not `/EN/tours`).
- `UseCulturePathRedirect`: redirects GET and HEAD requests without a supported prefix (`/`, `/tours`, `/fr/tours`) to the
  same local path under the best culture, with a 302. The culture is the remembered-choice cookie, then the Accept-Language
  header by quality, then the default culture. The response carries `Vary: Accept-Language, Cookie`. A first segment that
  names a supported culture in another spelling (`/EN/tours`, `/%20en/tours`) gets a 301 to the canonical segment
  (`/en/tours`) with the rest of the path and the query kept, so each page has exactly one URL. The target is only ever
  the path base, a supported culture, and the original local path and query, so it cannot leave the site. Paths under an
  excluded prefix (for example `/api`) pass through. Register it after static files.
- File paths: a path whose last segment contains a `.` (`/robots.txt`, `/css/site.css`, `/en/brochure.pdf`) is treated as a
  file and passes through without localization or redirect. `Slug` never contains `.`, so content paths built from slugs
  are always localized; do not end a localized route with a segment that contains `.`.
- `RememberCulture`: writes the remembered-choice cookie (HTTP-only, SameSite=Lax, one year) from a language-switch endpoint.
  See [The remembered-choice cookie](#the-remembered-choice-cookie).
- `CulturePath`: `TryParse`, `Prepend`, and `Switch` read, add, or swap the culture segment, for example to build
  language-switcher links.

## The remembered-choice cookie

`RememberCulture` stores only the culture name. Its path is the request's path base (`/` when empty), so a site hosted under
`/app` does not set a cookie for the whole host. `Secure` follows `Request.IsHttps`: behind a proxy or load balancer that
terminates TLS, configure forwarded headers (`UseForwardedHeaders` with `X-Forwarded-Proto`) before it runs, or the cookie is
sent without `Secure`. The cookie is marked `IsEssential = true`, which declares the language preference strictly necessary, so
a cookie-consent policy (`CookiePolicyOptions.CheckConsentNeeded`) does not suppress it; set your consent policy and privacy
notice accordingly.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. The placeholders do not identify a published package version.

```text
dotnet add package PandaBear.SharedKernel.AspNetCore.Localization --version <validated-candidate-version> --source <private-candidate-feed>
```

```csharp
using Microsoft.AspNetCore.Localization;
using PandaBear.AspNetCore.Localization;
using PandaBear.BuildingBlocks;

var supported = SupportedCultures.Create(
    ContentCulture.Restore("pt"),
    [ContentCulture.Restore("pt"), ContentCulture.Restore("en")]).Value;

var app = WebApplication.CreateBuilder(args).Build();
app.UseStaticFiles();
app.UseCulturePathRedirect(supported, excludedPrefixes: [new PathString("/api")]);
app.UseRequestLocalization(options =>
{
    options.RequestCultureProviders.Insert(0, new PathCultureRequestCultureProvider(supported));
});

// In a language switcher: the same page under another culture.
var switchPath = CulturePath.Switch(new PathString("/pt/tours/42"), ContentCulture.Restore("en"), supported); // /en/tours/42
```

## Package identity and availability

Package ID: `PandaBear.SharedKernel.AspNetCore.Localization`. C# namespace: `PandaBear.AspNetCore.Localization`. Assembly/project
identity: `SharedKernel.AspNetCore.Localization`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured
local/private feed. This README does not imply availability on nuget.org.
