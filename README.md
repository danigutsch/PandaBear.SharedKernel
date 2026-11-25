# PandaBear.SharedKernel

[![Build](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Reusable .NET building blocks for services and libraries: explicit results, generated mediation, typed HTTP outcomes, persistence integrations, and testing helpers.

**Preview:** APIs may change. Packages are not published to NuGet.org yet; use a validated preview candidate from a [successful build](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml).

## Find the right package

Choose the packages your project needs. Each package has its own README with requirements and examples.

| What you need | Start here |
| --- | --- |
| Represent success, validation failures, and optional values | [Results](src/SharedKernel/SharedKernel.Results/README.md) |
| Model domains and dispatch requests or events | [Domain](src/SharedKernel/SharedKernel.Domain/README.md) · [Mediator](src/SharedKernel/SharedKernel.Mediator/README.md) |
| Handle HTTP outcomes, uploads, caching, and API versions | [HttpClients](src/SharedKernel/SharedKernel.HttpClients/README.md) · [HTTP caching](src/SharedKernel/SharedKernel.HttpCaching.AspNetCore/README.md) · [API versioning](src/SharedKernel/SharedKernel.ApiVersioning/README.md) |
| Cache read models in memory and see edits from other hosts through a shared revision number | [Caching](src/SharedKernel/SharedKernel.Caching/README.md) |
| Secure and operate an ASP.NET Core host: headers, forwarded headers, CORS, error telemetry, JWT bearer authentication, BFF cookie sessions and user-token management, rate limiting, culture path prefixes, robots.txt and sitemaps, idempotency keys | [AspNetCore](src/SharedKernel/SharedKernel.AspNetCore/README.md) · [Authentication](src/SharedKernel/SharedKernel.AspNetCore.Authentication/README.md) · [BFF tokens](src/SharedKernel/SharedKernel.AspNetCore.Authentication.Bff/README.md) · [Rate limiting](src/SharedKernel/SharedKernel.AspNetCore.RateLimiting/README.md) · [Localization](src/SharedKernel/SharedKernel.AspNetCore.Localization/README.md) · [SEO](src/SharedKernel/SharedKernel.AspNetCore.Seo/README.md) · [Idempotency for ASP.NET Core](src/SharedKernel/SharedKernel.Idempotency.AspNetCore/README.md) |
| Count page views from the server into a self-hosted Umami, without a script, a cookie or the visitor's query values | [Web analytics](src/SharedKernel/SharedKernel.AspNetCore.WebAnalytics/README.md) · [Umami for Aspire](src/SharedKernel/SharedKernel.Aspire.Hosting.Umami/README.md) |
| Show HTTP failures, server validation, handled UI errors, upload status, and an image picker over a large library accessibly in Blazor | [Blazor](src/SharedKernel/SharedKernel.Blazor/README.md) |
| Let a site owner edit the public site's theme (colors, typography, header and shape) in an accessible admin form whose save errors land on their fields | [Site editing](src/SharedKernel/SharedKernel.Blazor.Site.Editing/README.md) |
| Add database-specific persistence and coordination | [Entity Framework Core](src/SharedKernel/SharedKernel.EntityFrameworkCore/README.md) · [PostgreSQL](src/SharedKernel/SharedKernel.Npgsql/README.md) · [PostgreSQL rules](src/SharedKernel/SharedKernel.PostgreSql/README.md) · [EF Core PostgreSQL](src/SharedKernel/SharedKernel.EntityFrameworkCore.Npgsql/README.md) · [SQLite](src/SharedKernel/SharedKernel.EntityFrameworkCore.Sqlite/README.md) · [Idempotency](src/SharedKernel/SharedKernel.Idempotency.Abstractions/README.md) |
| Work with durable events and integration messages | [Event sourcing](src/SharedKernel/SharedKernel.EventSourcing/README.md) · [Integration events](src/SharedKernel/SharedKernel.Messaging.IntegrationEvents/README.md) |
| Accept JPEG, PNG and WebP uploads by their bytes and strip location and camera metadata, and accept WOFF2 font uploads only when their structure is sound, without a native library | [Upload sanitizing](src/SharedKernel/SharedKernel.UploadSanitizing/README.md) |
| Rewind bounded uploads or imports for multiple passes, and write private files | [IO](src/SharedKernel/SharedKernel.IO/README.md) · [IO analyzers](src/SharedKernel/SharedKernel.IO.Analyzers/README.md) |
| Decode Bech32 (age keys) and encode Base32 (TOTP secrets) | [Encodings](src/SharedKernel/SharedKernel.Encodings/README.md) |
| Find credentials, keys, tokens, recovery codes, and seed phrases in text, and inspect and convert SSH private keys | [Secret scanning](src/SharedKernel/SharedKernel.SecretScanning/README.md) |
| Model photo albums: per-culture titles, slugs and publication, an ordered photo sequence with a cover, publish rules, and paged public views | [Galleries](src/SharedKernel/SharedKernel.Galleries/README.md) |
| Record reviewed content licensing, provenance, and public attribution for photos and other works | [Content rights](src/SharedKernel/SharedKernel.ContentRights/README.md) |
| Model sellable programmes such as tours: availability and dates, typed prices, conditions, app-defined metrics, steps, translated text with publish rules, public views, listing groups, display sentences and JSON-LD | [Offerings](src/SharedKernel/SharedKernel.Offerings/README.md) |
| Model typed page sections: tone, eyebrow and heading level, paragraphs, questions, a person, quotes, images, a video link and a button, with an application extension point and V2's validation, and render them with the accessible site components | [Page sections](src/SharedKernel/SharedKernel.PageSections/README.md) · [Page sections for Blazor](src/SharedKernel/SharedKernel.PageSections.Blazor/README.md) |
| Parse simple comma-delimited rows and detect duplicates | [Tabular](src/SharedKernel/SharedKernel.Tabular/README.md) |
| Model installed packages and plan what is missing, read-only | [Packages](src/SharedKernel/SharedKernel.Packages/README.md) |
| Wire Aspire hosts: service defaults with private telemetry and health endpoints, AppHost secrets, origins, health checks and image pins | [Service defaults](src/SharedKernel/SharedKernel.ServiceDefaults/README.md) · [Aspire hosting](src/SharedKernel/SharedKernel.Aspire.Hosting/README.md) |
| Build focused tests and container-backed integration tests | [Testing](src/SharedKernel/SharedKernel.Testing/README.md) · [Integration testing](src/SharedKernel/SharedKernel.IntegrationTesting/README.md) · [SQLite testing](src/SharedKernel/SharedKernel.Testing.Sqlite/README.md) |
| Keep secrets in git as age and SOPS files: validated recipients, creation rules, exact recipient-set checks, and stdin-only providers for age and sops | [Secrets](src/SharedKernel/SharedKernel.Secrets/README.md) · [age](src/SharedKernel/SharedKernel.Secrets.Age/README.md) · [sops](src/SharedKernel/SharedKernel.Secrets.Sops/README.md) |
| Automate documentation, package maintenance, and CI validation | [Documentation tool](tools/SharedKernel.Documentation.Tool/README.md) · [Versioning tool](tools/SharedKernel.Versioning.Tool/README.md) · [CI tool](tools/SharedKernel.CI.Tool/README.md) |
| Run configured k6 load scenarios against local targets | [k6 tool](tools/SharedKernel.K6.Tool/README.md) |
| Probe what a sandboxed environment allows and what that means for .NET tools | [Sandbox tool](tools/SharedKernel.Sandbox.Tool/README.md) |
| Compare declared packages with installed ones, one owner per package | [Packages tool](tools/SharedKernel.Packages.Tool/README.md) |
| Generate age keys; encrypt, decrypt, and edit SOPS files; update recipients and rotate data keys; and verify every file's recipients against its creation rule | [Secrets tool](tools/SharedKernel.Secrets.Tool/README.md) |
| Merge and migrate password vault exports offline | [Vault manager tool](tools/SharedKernel.VaultManager.Tool/README.md) |

## Try it

Download the unchanged `.nupkg` files and `candidate.json` from a candidate's GitHub prerelease, `v<packageVersion>`, or from the `candidate-packages` artifact of a successful CI run on `main`, into a local feed directory. NuGet registry publication and stable promotion remain deferred. Verify the clean source checkout matches the manifest revision and every package SHA-256 matches the trusted candidate manifest before consumption. Replace the placeholders below with that candidate's version and directory:

```text
dotnet add package PandaBear.SharedKernel.Results --version <preview-version> --source <candidate-feed>
```

Make expected outcomes explicit:

```csharp
using SharedKernel.Results;

public static class Example
{
    public static Result<int> ParseCount(string text)
        => int.TryParse(text, out var value) && value >= 0
            ? Result.Ok(value)
            : Result.Invalid<int>("Count is invalid.", "count", "Use a non-negative integer.");
}
```

Package IDs start with `PandaBear.SharedKernel.`; C# namespaces, like assemblies and projects, start with `SharedKernel.`. The [Results sample](samples/Results/BasicResults.Sample/README.md) shows composition, optional values, and asynchronous flows.

## Build from source

Install the .NET SDK pinned in [global.json](global.json), then clone and build:

```sh
git clone https://github.com/danigutsch/PandaBear.SharedKernel.git
cd PandaBear.SharedKernel
bash setup-dev.sh
```

On Windows, run `pwsh -File setup-dev.ps1` instead. Run the tests with:

```sh
dotnet test --solution SharedKernel.slnx -c Release --no-build --no-restore
```

Provider integration tests need Docker or a compatible Podman runtime. NativeAOT checks also need the platform's native build tools; see [development prerequisites](docs/development.md).

The libraries favor typed APIs and existing source generators over runtime reflection where practical. AOT and trimming support depend on the package and its providers; check the package README for its supported boundary.

## Learn more

- [Documentation and architecture](docs/README.md)
- [Package development, coverage, and validation](docs/package-development.md)
- [Preview versions and release policy](docs/release-policy.md)
- [Contributing](CONTRIBUTING.md) · [Security](SECURITY.md) · [License](LICENSE.txt)
