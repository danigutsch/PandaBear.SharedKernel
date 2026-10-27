# SharedKernel.AspNetCore

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable ASP.NET Core helpers for applications that explicitly opt into shared hosting and security
mechanics.

The package does not define application policy names, origin lists, rate limits, roles, claims, or
content security policies. Each consuming application owns those decisions.

A host that co-hosts modules with different API audiences passes `ApiBearerScheme` values to
`AddApiSecurity`. Each scheme accepts only tokens for its single audience and maps roles with its own
permission table when it validates a token; names and audiences must be unique. No scheme authenticates by
default: every protected route's policy must name its scheme with `AddAuthenticationSchemes(name)`, and the
fallback policy rejects a route that does not, so one module's token never satisfies another module's route.
The first scheme only issues challenges and forbids.

Use `MapRobotsTxt` to expose an application-owned crawler policy at `/robots.txt` with
`text/plain; charset=utf-8`.

Use `SitemapXmlSerializer` to generate sitemap XML while retaining URL inclusion policy in the
consuming application.

Use `IdempotencyKeyHeader.Create(value)` to create a required `IdempotencyKey` from HTTP input as a
`Result<IdempotencyKey>`. The value object owns token rules and normalization; this adapter associates
missing or invalid input with the standard `Idempotency-Key` validation field. It does not generate
keys, coordinate operations, or log header values.

Use `HostEnvironmentVariableNames.AspNetCoreEnvironment` and
`HostEnvironmentVariableNames.DotNetEnvironment` for C# environment-variable lookups in web hosts.
These constants name host integration inputs without choosing an environment or application policy.
Their canonical ownership is registered in the [governed-literal catalog](../SharedKernel.Style.Analyzers/GovernedLiterals.txt).

## Error and security telemetry

Opt in with `services.AddUnhandledExceptionHandler()` (and `app.UseExceptionHandler()`) for a
`traceId`-only RFC 9457 error body plus one log event and the `unhandled_exceptions_total` counter, and with
`services.AddSecurityEventTelemetry()` for bounded JWT bearer failure, challenge and forbidden events and rate-limit
rejections. Exception messages, tokens and claims never reach the response, log or metric tags. See
[ASP.NET Core error and security telemetry](../../../docs/aspnetcore-error-and-security-telemetry.md) for names, tags
and alert rule templates.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.AspNetCore`. C# namespace: `PandaBear.AspNetCore`. Assembly/project identity: `SharedKernel.AspNetCore`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.

## Subject identity

Use `principal.GetSubjectId()` to read the opaque subject from a validated principal.
`ApiAuthenticationDefaults.SubjectClaimType` supplies the standard JWT claim name. Missing or
blank subjects return null; name identifiers, email and display names are never fallback audit IDs.
The helper does not authenticate or authorize requests. Keep token validation, claim mapping and
permission policies in the host, and pass the extracted identifier explicitly into application commands.

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
