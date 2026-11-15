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
  segment (`/en/tours`, not `/EN/tours`). The request culture is the segment, or, when the provider is created with
  `CultureLanguageTags`, the language tag the segment's culture declares (`/pt/tours` gives `pt-BR`). See
  [Language tags other than the segment](#language-tags-other-than-the-segment).
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

## Language tags other than the segment

A site can keep short addresses and content keys (`/pt/...`, stored under `pt`) while its pages declare a regional language
(`pt-BR`) in `<html lang>`, `Content-Language`, `hreflang`, and `og:locale`, and format numbers and dates with it. Describe the
mapping once with `CultureLanguageTags` from `PandaBear.SharedKernel.BuildingBlocks` and pass it to the provider. Only the
cultures that declare another tag are listed; the others declare themselves, so without a mapping the behavior is the same as
the `SupportedCultures` constructor. `CultureLanguageTags.Create` rejects a culture that is not supported, a culture with two
tags, and a tag that two cultures would declare, so `.Value` stops the application at start-up with that message.

Register the tags, not the segments, as the supported cultures and UI cultures: the middleware accepts only a provider result it
supports, and would otherwise fall back to a parent (`pt`) or the default. The address segment stays the culture:
`/pt-BR/tours` is not a localized address, and `CulturePath`, `UseCulturePathRedirect`, and `RememberCulture` keep working with
the `SupportedCultures` set. The redirect matches `Accept-Language` against the segments with `SupportedCultures.Resolve`, so a
tag that refines its segment's language (`pt-BR` for `pt`) is matched through its parent.

```csharp
var pt = ContentCulture.Restore("pt");
var en = ContentCulture.Restore("en");
var supported = SupportedCultures.Create(pt, [pt, en]).Value;
var languageTags = CultureLanguageTags.Create(supported, [new CultureLanguageTag(pt, ContentCulture.Restore("pt-BR"))]).Value;
string[] requestCultures = [.. languageTags.LanguageTags.Select(static tag => tag.Value)]; // pt-BR, en

app.UseRequestLocalization(options =>
{
    options.SetDefaultCulture(languageTags.DefaultLanguageTag.Value)
        .AddSupportedCultures(requestCultures)
        .AddSupportedUICultures(requestCultures);
    options.ApplyCurrentCultureToResponseHeaders = true;
    options.RequestCultureProviders = [new PathCultureRequestCultureProvider(languageTags)];
});

// A page under /pt/ runs as pt-BR. Build its links from the culture, and declare the tag:
var page = languageTags.TryGetCulture(CultureInfo.CurrentUICulture.Name, out var culture) ? culture : supported.Default; // pt
var english = CulturePath.Switch(new PathString("/pt/tours/42"), en, supported);                                         // /en/tours/42
var hreflang = languageTags.LanguageTagOf(en).Value;                                                                     // en
```

The head and link helpers take the language tag to declare, not the segment: `AlternateLink`, `LanguageLink`, and
`PageHead.Locale` in `PandaBear.SharedKernel.Blazor.Site`, and `SitemapAlternate` in `PandaBear.SharedKernel.AspNetCore.Seo`.
Pass `languageTags.LanguageTagOf(culture)`.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.AspNetCore.Localization`. C# namespace: `PandaBear.AspNetCore.Localization`. Assembly/project
identity: `SharedKernel.AspNetCore.Localization`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured
local/private feed. This README does not imply availability on nuget.org.
