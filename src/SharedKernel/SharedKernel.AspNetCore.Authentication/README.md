# SharedKernel.AspNetCore.Authentication

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

JWT bearer API authentication, permission-based authorization, subject-claim helpers, an antiforgery-protected
sign-out endpoint and authentication-event telemetry for ASP.NET Core. This is the only SharedKernel ASP.NET Core
package that depends on `Microsoft.AspNetCore.Authentication.JwtBearer`.

The package does not define application policy names, roles, claims, or token lifetimes. Each consuming
application owns those decisions and passes them in explicitly.

## What is inside

- `AddApiSecurity`, `AddApiBearerAuthentication`, `AddApiAuthorization` and `UseApiSecurity`: JWT bearer setup from the
  `Authentication:Authority` / `Authentication:Issuer` configuration keys, with a permission table per role.
- `ApiBearerScheme`: a host that co-hosts modules with different API audiences passes one value per audience. Each scheme
  accepts only tokens for its single audience and maps roles with its own permission table; names and audiences must be
  unique. No scheme authenticates by default: every protected route's policy must name its scheme with
  `AddAuthenticationSchemes(name)`, and the fallback policy rejects a route that does not.
- `ApiAuthenticationDefaults`: configuration keys and provider-neutral claim types.
- `AuthorizationPolicyBuilderExtensions.RequirePermission` and `ClaimsPrincipalExtensions.GetSubjectId`. The subject
  helper returns the opaque subject of a validated principal; missing or blank subjects return null, and name
  identifiers, email and display names are never fallback audit IDs.
- `MapAntiforgeryProtectedSignOut`: an antiforgery-protected sign-out endpoint for a local and a remote scheme.
- `AddAuthenticationEventTelemetry`: bounded log and metric events for JWT bearer failures, challenges and forbidden
  results. Tokens, claims and exception messages never reach logs or metric tags. See
  [ASP.NET Core error and security telemetry](../../../docs/aspnetcore-error-and-security-telemetry.md).

## Install

```sh
dotnet add package PandaBear.SharedKernel.AspNetCore.Authentication
```

```csharp
using PandaBear.AspNetCore.Authentication;

builder.Services.AddApiSecurity(
    builder.Configuration,
    builder.Environment,
    audience: "orders-api",
    permissionsByRole: new Dictionary<string, IReadOnlyCollection<string>>
    {
        ["admin"] = ["orders.read", "orders.write"],
    });
builder.Services.AddAuthenticationEventTelemetry();

var app = builder.Build();
app.UseApiSecurity();
```

## Migrating from SharedKernel.AspNetCore

All types moved out of `PandaBear.SharedKernel.AspNetCore` (namespace `PandaBear.AspNetCore`) into this package and
namespace `PandaBear.AspNetCore.Authentication`: `ApiAuthenticationDefaults`, `ApiAuthenticationServiceCollectionExtensions`,
`ApiBearerScheme`, `ApiSecurityApplicationBuilderExtensions`, `AuthorizationPolicyBuilderExtensions`,
`ClaimsPrincipalExtensions` and `AntiforgerySignOutEndpointRouteBuilderExtensions`. Add the package reference and
replace `using PandaBear.AspNetCore;` with `using PandaBear.AspNetCore.Authentication;` where these types are used.

`AddSecurityEventTelemetry()` was split by concern: call `AddAuthenticationEventTelemetry()` from this package for the JWT
bearer events and `AddRateLimitEventTelemetry()` from `PandaBear.SharedKernel.AspNetCore.RateLimiting` for rate-limit
rejections. Meter, instrument, tag and log event names are unchanged.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.AspNetCore.Authentication`. C# namespace: `PandaBear.AspNetCore.Authentication`.
Assembly/project identity: `SharedKernel.AspNetCore.Authentication`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured
local/private feed. This README does not imply availability on nuget.org.
