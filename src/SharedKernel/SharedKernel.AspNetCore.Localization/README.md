# SharedKernel.AspNetCore.Localization

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Path-prefix localization for ASP.NET Core sites whose URLs start with the culture (`/pt/tours`, `/en/tours`). The consuming
application owns the list of languages, the excluded routes, and the translated content; this package supplies the
mechanics. It builds on the `SupportedCultures` and `ContentCulture` value objects of
`PandaBear.SharedKernel.BuildingBlocks`.

## What is inside

- `PathCultureRequestCultureProvider`: a `RequestCultureProvider` that reads the first path segment against a
  `SupportedCultures` set, for `UseRequestLocalization`.
- `UseCulturePathRedirect`: redirects GET and HEAD requests without a supported prefix (`/`, `/tours`, `/fr/tours`) to the
  same local path under the best culture, with a 302. The culture is the remembered-choice cookie, then the Accept-Language
  header by quality, then the default culture. The response carries `Vary: Accept-Language, Cookie`. The target is only ever
  the path base, a supported culture, and the original local path and query, so it cannot leave the site. Paths whose last
  segment has a file extension (`/robots.txt`, `/css/site.css`) and paths under an excluded prefix (for example `/api`)
  pass through. Register it after static files.
- `RememberCulture`: writes the remembered-choice cookie (HTTP-only, SameSite=Lax, one year) from a language-switch endpoint.
- `CulturePath`: `TryParse`, `Prepend`, and `Switch` read, add, or swap the culture segment, for example to build
  language-switcher links.

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
