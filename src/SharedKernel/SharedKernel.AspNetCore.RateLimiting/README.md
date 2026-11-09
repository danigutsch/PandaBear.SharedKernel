# SharedKernel.AspNetCore.RateLimiting

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Remote-IP fixed-window rate-limit policies and rate-limit rejection telemetry for ASP.NET Core. The package does not
define policy names or limits; each consuming application owns them.

## What is inside

- `AddRemoteIpFixedWindowPolicy` and `AddRemoteIpFixedWindowPolicies` (with `RemoteIpFixedWindowRateLimitPolicy`): fixed-window
  policies partitioned by remote IP address, with no queueing and status code 429. IPv4-mapped IPv6 addresses are normalized
  to IPv4. Applications behind reverse proxies must configure and trust forwarded headers before the limiter runs (see
  `AddConfiguredTrustedForwardedHeaders` in `PandaBear.SharedKernel.AspNetCore`).
- `AddRateLimitEventTelemetry`: logs and counts each rejection with the policy name; an existing `OnRejected` callback keeps
  running afterwards. See [ASP.NET Core error and security telemetry](../../../docs/aspnetcore-error-and-security-telemetry.md).

## Install

```sh
dotnet add package PandaBear.SharedKernel.AspNetCore.RateLimiting
```

```csharp
using PandaBear.AspNetCore.RateLimiting;

builder.Services.AddRateLimiter(options =>
    options.AddRemoteIpFixedWindowPolicy("login", permitLimit: 5, window: TimeSpan.FromMinutes(1)));
builder.Services.AddRateLimitEventTelemetry();

var app = builder.Build();
app.UseRateLimiter();
app.MapPost("/login", () => Results.Ok()).RequireRateLimiting("login");
```

## Migrating from SharedKernel.AspNetCore

`AspNetCoreRateLimitingExtensions` and `RemoteIpFixedWindowRateLimitPolicy` moved from `PandaBear.SharedKernel.AspNetCore`
(namespace `PandaBear.AspNetCore`) into this package and namespace `PandaBear.AspNetCore.RateLimiting`. Add the package
reference and replace `using PandaBear.AspNetCore;` with `using PandaBear.AspNetCore.RateLimiting;` where they are used.

`AddSecurityEventTelemetry()` was split by concern: call `AddRateLimitEventTelemetry()` from this package for rate-limit
rejections and `AddAuthenticationEventTelemetry()` from `PandaBear.SharedKernel.AspNetCore.Authentication` for JWT bearer
events. Meter, instrument, tag and log event names are unchanged.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.AspNetCore.RateLimiting`. C# namespace: `PandaBear.AspNetCore.RateLimiting`.
Assembly/project identity: `SharedKernel.AspNetCore.RateLimiting`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured
local/private feed. This README does not imply availability on nuget.org.
