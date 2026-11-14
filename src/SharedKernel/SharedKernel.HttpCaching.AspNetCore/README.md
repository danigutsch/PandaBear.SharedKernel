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

## Default public caching

`app.UseDefaultPublicCaching(TimeSpan.FromSeconds(60))` lets browsers and proxies keep anonymous pages briefly. When a response
starts, it adds `Cache-Control: public, max-age=60` only if all of these hold: the request is GET or HEAD, the status is 200, the
response has no `Cache-Control` of its own, the response sets no cookie, it does not vary by `Cookie` or `*`, its content security policy holds no per-request
`'nonce-…'` source, the request has no `Authorization` header, and the user is not authenticated. Every other response is left untouched, so personalized, failed and redirected answers are never made public,
and endpoints that choose their own lifetime keep it. The lifetime must be at least one second and is written in whole seconds.
Register it before the endpoints; the user is checked when the response starts, so later authentication is taken into account.
Request cookies are not checked: a page whose content depends on a cookie says so with `Vary: Cookie`.
The nonce check sees only a policy that is already written when this middleware decides; `UseSecurityHeaders` from
`PandaBear.SharedKernel.AspNetCore` turns a `public` header on its nonce responses into `private` itself, so the two can be
registered in either order.

## Immutable responses

`HttpCacheHeaders.SetImmutable(httpContext)` writes `Cache-Control: public, max-age=31536000, immutable`
(`HttpCacheHeaders.ImmutableCacheControl`) and removes `Pragma` and `Expires`. Use it only for bytes whose URL changes with their
content, such as a content-hashed logo or upload. It sets no other header; serve such files with `X-Content-Type-Options: nosniff`
as any other upload (`MapImmutableFiles` in `PandaBear.SharedKernel.AspNetCore` does both).

## If-Match preconditions

`IfMatchCondition` is the RFC 9110 `If-Match` condition for a write guarded by an opaque entity tag: `*` or a list of entity tags,
compared strongly, so a weak tag never matches. `TryParse` refuses the whole value when an element is malformed, when it is empty, or
when `*` is combined with tags. `Matches(current)` is false when the resource does not exist (`null`).

```csharp
if (IfMatchCondition.Read(httpContext.Request, out var ifMatch) is { } problem)
{
    return problem; // 428 without If-Match, 400 for an unusable one
}

var current = await store.Get(ct);
if (!ifMatch!.Matches(current?.ETag))
{
    return PreconditionProblems.Failed(); // 412
}
```

`PreconditionProblems.Required` (428), `InvalidIfMatch` (400) and `Failed` (412) take an optional detail and extension members, such as
an application error code, when the defaults do not fit. A numeric version carried in `If-Match` is simpler with `ConcurrencyHeaders` in
`PandaBear.SharedKernel.Results.AspNet`, which answers 400 and leaves a stale version to the operation. Document the header and the
problems with `WithIfMatchPrecondition()` and `WithEntityTagResponseHeader()` from `PandaBear.SharedKernel.OpenApi`, which already
references the OpenAPI packages that this one does not.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.HttpCaching.AspNetCore`. C# namespace: `PandaBear.HttpCaching.AspNetCore`. Assembly/project identity: `SharedKernel.HttpCaching.AspNetCore`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
