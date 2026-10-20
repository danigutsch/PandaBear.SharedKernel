# SharedKernel.HttpCaching.AspNetCore

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

ASP.NET Core helpers for small, explicit HTTP cache response-header policies.

## Contract

- `WithNoStoreResponses` adds an endpoint filter that calls `HttpCacheHeaders.SetNoStore` before
  each executed endpoint in the route group.
- The policy writes `Cache-Control: no-store`, `Pragma: no-cache`, and an expired `Expires` header.
- Use it for management or other non-cacheable route groups. Endpoint code that writes cache
  headers later can replace the policy.

## Scoped route-group protection

Use `WithNoStoreResponses(pathPrefix)` on a route group and register `app.UseNoStoreResponses()`
before authentication, authorization, and rate limiting. Supply the absolute literal group prefix,
including parent route groups. Configuration stays with the group; the pipeline contains no
application-specific path checks.

The middleware consumes endpoint metadata and the existing `EndpointDataSource`. It also matches
configured prefixes on segment boundaries so unmatched paths and unsupported HTTP methods retain
no-store headers. Headers are enforced when the response starts, including exception responses and
responses that attempt to set public cache headers. No separate policy registry or cache is created.

The parameterless endpoint-filter overload remains available for executed-endpoint-only policies.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.HttpCaching.AspNetCore`. C# namespace: `PandaBear.HttpCaching.AspNetCore`. Assembly/project identity: `SharedKernel.HttpCaching.AspNetCore`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
