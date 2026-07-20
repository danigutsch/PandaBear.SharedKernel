# SharedKernel.HttpCaching.AspNetCore

ASP.NET Core helpers for small, explicit HTTP cache response-header policies.

## Contract

- `WithNoStoreResponses` adds an endpoint filter that calls `HttpCacheHeaders.SetNoStore` before
  each executed endpoint in the route group.
- The policy writes `Cache-Control: no-store`, `Pragma: no-cache`, and an expired `Expires` header.
- Use it for management or other non-cacheable route groups. Endpoint code that writes cache
  headers later can replace the policy.
