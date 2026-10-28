# SharedKernel.Testing.AspNetCore

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

ASP.NET Core web application factory helpers for SharedKernel tests.

## Purpose

This package centralizes small `WebApplicationFactory<TEntryPoint>` setup helpers that are reusable
across ASP.NET Core test projects.

## Scope

- Create test hosts for ASP.NET Core entry point assemblies
- Optionally set the hosting environment
- Optionally apply test service overrides

## Fakes

- `FakeFormFile`: in-memory `IFormFile`.
- `FakeBrowserFile`: in-memory Blazor `IBrowserFile` that rejects reads above `maxAllowedSize` with `IOException`.
- `ThrowingDistributedCache`: `IDistributedCache` whose every operation throws the supplied exception.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.AspNetCore`. C# namespace: `PandaBear.Testing.AspNetCore`. Assembly/project identity: `SharedKernel.Testing.AspNetCore`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.

## Invalid request bodies in tests

`WebApplicationTestHost.Create` configures `RouteHandlerOptions.ThrowOnBadRequest = false` inside
its test-service callback. Malformed bodies receive HTTP 400 in Development and Production test
hosts, without executing their endpoint handler. Regular application starts retain ASP.NET Core's
environment-specific defaults; this package does not change production or Development startup.

For a manually constructed TestServer or WebApplication test host, use the same policy:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using PandaBear.Testing.AspNetCore;

var builder = WebApplication.CreateBuilder();
builder.WebHost.UseTestServer();
builder.Services.AddTestRequestBindingDefaults();
```

Tests that specifically inspect binding exceptions can opt out with their normal service callback:
`services.PostConfigure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true)`.
The default is registered before caller overrides. This controls rejection behavior; it never
accepts or retains an invalid body.

## Output-cache eviction

`RecordingOutputCacheStore` implements `IOutputCacheStore` for tests interested in eviction.
It records `EvictionObserved`, the last `EvictionTag`, and `EvictionCancellationToken` before
throwing an optional `EvictionException`. Reads always miss and writes do nothing; it does not
simulate a functioning cache. Create a separate instance for each independently running scenario.
