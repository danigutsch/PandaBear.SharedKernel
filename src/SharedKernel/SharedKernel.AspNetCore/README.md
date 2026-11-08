# SharedKernel.AspNetCore

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable ASP.NET Core helpers for applications that explicitly opt into shared hosting and security mechanics. This is
the light core: it depends on the ASP.NET Core shared framework and `SharedKernel.InputNormalization` only.

The package does not define application policy names, origin lists, content security policies, or other application
policy. Each consuming application owns those decisions.

## What is inside

- `UseSecurityHeaders`: common browser security headers with an application-owned Content Security Policy and optional
  Permissions Policy.
- `AddConfiguredTrustedForwardedHeaders` and `ConfigureTrustedForwardedHeaders`: forwarded-header trust from
  configuration. `AddForwardedClientIp` forwards the resolved visitor identity to a downstream client (see below).
- `AddConfiguredOriginsPolicy`: a CORS policy from a configuration section of allowed origins.
- `AddUnhandledExceptionHandler` and `AspNetCoreTelemetry`: an opt-in unhandled-exception handler and the names of the
  meter, instruments, tags and log event identifiers of the ASP.NET Core telemetry family.
- `HostEnvironmentVariableNames.AspNetCoreEnvironment` and `HostEnvironmentVariableNames.DotNetEnvironment` for C#
  environment-variable lookups in web hosts. These constants name host integration inputs without choosing an
  environment or application policy. Their canonical ownership is registered in the
  [governed-literal catalog](../SharedKernel.Style.Analyzers/GovernedLiterals.txt).

## Related packages

The concerns that need extra dependencies live in their own packages:

| Need | Package | Namespace |
| --- | --- | --- |
| JWT bearer authentication, permission policies, claims helpers, antiforgery sign-out | [`PandaBear.SharedKernel.AspNetCore.Authentication`](../SharedKernel.AspNetCore.Authentication/README.md) | `PandaBear.AspNetCore.Authentication` |
| Remote-IP fixed-window rate limiting | [`PandaBear.SharedKernel.AspNetCore.RateLimiting`](../SharedKernel.AspNetCore.RateLimiting/README.md) | `PandaBear.AspNetCore.RateLimiting` |
| `robots.txt` and sitemap XML | [`PandaBear.SharedKernel.AspNetCore.Seo`](../SharedKernel.AspNetCore.Seo/README.md) | `PandaBear.AspNetCore.Seo` |
| `Idempotency-Key` header and key-bound data protector | [`PandaBear.SharedKernel.Idempotency.AspNetCore`](../SharedKernel.Idempotency.AspNetCore/README.md) | `PandaBear.Idempotency.AspNetCore` |

## Install

```sh
dotnet add package PandaBear.SharedKernel.AspNetCore
```

```csharp
using PandaBear.AspNetCore;

builder.Services.AddUnhandledExceptionHandler();
builder.Services.AddConfiguredTrustedForwardedHeaders(builder.Configuration);

var app = builder.Build();
app.UseExceptionHandler();
app.UseSecurityHeaders(contentSecurityPolicy: "default-src 'self'");
```

## Migrating from the previous single package

Before this split, `PandaBear.SharedKernel.AspNetCore` carried every concern below in the namespace `PandaBear.AspNetCore`.
The package id is unchanged and shrank to the core listed above; there is no meta-package. Add the package that now owns
each type you use and update the `using` directive:

| Types | Now in package | New namespace |
| --- | --- | --- |
| `ApiAuthenticationDefaults`, `ApiAuthenticationServiceCollectionExtensions`, `ApiBearerScheme`, `ApiSecurityApplicationBuilderExtensions`, `AuthorizationPolicyBuilderExtensions`, `ClaimsPrincipalExtensions`, `AntiforgerySignOutEndpointRouteBuilderExtensions` | `PandaBear.SharedKernel.AspNetCore.Authentication` | `PandaBear.AspNetCore.Authentication` |
| `AspNetCoreRateLimitingExtensions`, `RemoteIpFixedWindowRateLimitPolicy` | `PandaBear.SharedKernel.AspNetCore.RateLimiting` | `PandaBear.AspNetCore.RateLimiting` |
| `AspNetCoreRobotsTxtExtensions`, `SitemapCanonicalOrigin`, `SitemapEntry`, `SitemapXmlSerializer` | `PandaBear.SharedKernel.AspNetCore.Seo` | `PandaBear.AspNetCore.Seo` |
| `IdempotencyKeyHeader`, `KeyBoundDataProtector` | `PandaBear.SharedKernel.Idempotency.AspNetCore` | `PandaBear.Idempotency.AspNetCore` |

`AddSecurityEventTelemetry()` was split by concern: `AddAuthenticationEventTelemetry()` (Authentication) and
`AddRateLimitEventTelemetry()` (RateLimiting). Meter, instrument, tag and log event names stay in `AspNetCoreTelemetry`
and are unchanged. Security headers, forwarded headers, CORS, the unhandled-exception handler, `AspNetCoreTelemetry` and
`HostEnvironmentVariableNames` keep their package and namespace.

## Error and security telemetry

Opt in with `services.AddUnhandledExceptionHandler()` (and `app.UseExceptionHandler()`) for a
`traceId`-only RFC 9457 error body plus one log event and the `unhandled_exceptions_total` counter. Bounded JWT bearer
failure, challenge and forbidden events come from `AddAuthenticationEventTelemetry()` in the Authentication package and
rate-limit rejections from `AddRateLimitEventTelemetry()` in the RateLimiting package. Exception messages, tokens and
claims never reach the response, log or metric tags. See
[ASP.NET Core error and security telemetry](../../../docs/aspnetcore-error-and-security-telemetry.md) for names, tags
and alerting guidance.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.AspNetCore`. C# namespace: `PandaBear.AspNetCore`. Assembly/project identity: `SharedKernel.AspNetCore`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.

## Forward resolved visitor identity

Opt an individual downstream client into `AddForwardedClientIp()` when the receiving service
explicitly trusts this application's proxy address:

```csharp
builder.AddConfiguredTrustedForwardedHeaders();
builder.Services.AddHttpClient<IPublicCatalogClient, PublicCatalogClient>()
    .AddForwardedClientIp();
```

Run inbound forwarded-header middleware before endpoints call the client. The handler reads the
current resolved `HttpContext.Connection.RemoteIpAddress` on every send, overwrites outgoing
`X-Forwarded-For`, and never copies raw incoming forwarding headers. IPv4-mapped IPv6 addresses
are normalized to IPv4. Without a current HTTP request or remote address, stale outgoing forwarding
is removed and the downstream service retains its bounded peer-IP policy.

The receiving service must independently configure its trusted proxy addresses/networks and run
forwarded-header middleware before rate limiting. This helper does not authenticate a service,
trust arbitrary peers, set policy budgets, or change other HTTP clients. Keep visitor admission,
aggregate capacity limits, and treatment of per-visitor quota responses in the consuming application.
