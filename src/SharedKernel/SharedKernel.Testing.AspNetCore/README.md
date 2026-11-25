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
- Never watch configuration files: `WebApplicationTestHost` and `KestrelTestHost` (and so `KestrelPageTest` of
  SharedKernel.Testing.Playwright) set `hostBuilder:reloadConfigOnChange=false`, because one appsettings watcher per file and host
  exhausts the per-user inotify limit when many test hosts run in parallel. A `configuration` value for that key turns it back on.
- Log to the test, not to the console: the hosts replace the application's console logger with `TestOutputLoggerProvider`
  (SharedKernel.Testing.Xunit). An entry goes to the output of the test it belongs to, which the runner shows only when that
  test fails; an entry logged on a server or background thread belongs to the test that created the host. `dotnet test`
  (.NET 11 SDK) streams what a test process writes to its console for every test, passing or not, so a console logger would
  print every log line of every run. The `configureTestServices` callback runs after the replacement and can add providers.

## Service overrides

For the `configureTestServices` callback:

- `services.RemoveHostedServices()` removes every `IHostedService` the application registered (background jobs, consumers); the web
  server keeps running.
- `services.ClearHealthChecks()` removes every health check registration, including later ones, so the host does not probe
  dependencies the test never starts.

## Fakes

- `FakeFormFile`: in-memory `IFormFile`.
- `StreamFormFile`: `IFormFile` over a caller-supplied stream with a declared `Length`. `OpenReadStream` returns that same
  stream on every call without buffering or rewinding it, so a non-seekable, slow or failing source (such as
  `TrackingReadStream` from SharedKernel.Testing) reaches the code under test unchanged, and the declared length can
  misstate the content. The caller owns the stream.
- `FakeBrowserFile`: in-memory Blazor `IBrowserFile` that rejects reads above `maxAllowedSize` with `IOException`.
- `ThrowingDistributedCache`: `IDistributedCache` whose every operation throws the supplied exception.

## Real sockets for browsers

`KestrelTestHost.Start<TEntryPoint>(environment, configuration, configureTestServices)` runs the application's real entry point on
Kestrel at `127.0.0.1` with an ephemeral port and returns a host with `BaseAddress` and `Services`; dispose it when done. Use it
when the client is out of process, such as a browser; in-memory `WebApplicationFactory` servers cannot be reached over a socket.

`KestrelTestHost.StartFromFactory(factory)` starts a derived factory when the test already has
application-specific `WebApplicationFactory` overrides. Dispose the running host before the
original factory.

## Worker entry-point registration

`WorkerTestHost.Build` invokes a managed executable entry point and returns the Generic Host before it
starts. Use it to verify the registrations in a worker or command-line app's actual composition.
Pass the executable assembly and its arguments. The caller owns the returned host.

```csharp
using var host = WorkerTestHost.Build(typeof(WorkerMarker).Assembly, "--environment=Production");
var registration = host.Services.GetRequiredService<WorkerRegistration>();
```

Capture uses the Generic Host diagnostic event in the entry point's execution context. It expects a
trusted test executable that reaches host construction or exits. If startup fails after a host was
captured, the helper disposes that host and preserves the startup failure. A simultaneous cleanup
failure is reported alongside it.

## Prerendered Blazor state

A prerendered Interactive Server page writes the state of its `[PersistentState]` properties into the HTML, and the browser sends
it back in the message that starts the circuit. SignalR closes a connection whose incoming message exceeds
`HubOptions.MaximumReceiveMessageSize` (32 KB by default), so a page whose state outgrows the limit never becomes interactive; the
browser only logs "Server returned an error on close". Microsoft's guidance is to put less data into the prerendered state rather
than raise the limit ([Prerendered state size and SignalR message size limit](https://learn.microsoft.com/aspnet/core/blazor/fundamentals/signalr?view=aspnetcore-11.0#prerendered-state-size-and-signalr-message-size-limit));
page or search large lists instead (`PageWindow` in `PandaBear.SharedKernel.BuildingBlocks`, `Pager` in
`PandaBear.SharedKernel.Blazor.Site`, `OptionSearch<TOption>` in `PandaBear.SharedKernel.Blazor`).

`PrerenderedComponentState` turns that into a test. Fetch a page with data that resembles production, the way a browser would, and
check its state:

```csharp
await using var host = KestrelTestHost.Start<Program>();
using var client = new HttpClient { BaseAddress = host.BaseAddress };
var html = await client.GetStringAsync(new Uri("/images", UriKind.Relative), cancellationToken);

PrerenderedComponentState.ShouldKeepServerStateBelow(html);
```

- `ServerStateSize(html)` and `WebAssemblyStateSize(html)` return the UTF-8 bytes inside the `<!--Blazor-Server-Component-State:…-->`
  and `<!--Blazor-WebAssembly-Component-State:…-->` comments (`ServerStateMarker`, `WebAssemblyStateMarker`; the markers that .NET 11
  writes), added up over every comment of that kind, and 0 for a page without state. An unclosed comment throws `FormatException`.
- `ShouldKeepServerStateBelow(html)` throws `InvalidOperationException` unless the server state is below
  `DefaultMaximumReceiveMessageSize`, which reads SignalR's own default (32,768 bytes) from the framework's `HubOptionsSetup` instead of
  copying the number. The message names the size, the limit and `GuidanceUrl`. A page without state passes. Pass a `limit` when the
  application configures another `MaximumReceiveMessageSize`, or a lower one for headroom: the start message also carries the
  component descriptors.
- Only the server state counts against the circuit; the WebAssembly state is measured for reporting but not checked.

Read the HTML as the server sent it (`HttpClient`, or a Playwright response's `TextAsync`), not `IPage.ContentAsync` after the
Blazor script has run.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.AspNetCore`. C# namespace: `SharedKernel.Testing.AspNetCore`. Assembly/project identity: `SharedKernel.Testing.AspNetCore`.

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
using SharedKernel.Testing.AspNetCore;

var builder = WebApplication.CreateBuilder();
builder.WebHost.UseTestServer();
builder.Services.AddTestRequestBindingDefaults();
```

Tests that specifically inspect binding exceptions can opt out with their normal service callback:
`services.PostConfigure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true)`.
The default is registered before caller overrides. This controls rejection behavior; it never
accepts or retains an invalid body.

With the opt-out the endpoint throws `BadHttpRequestException`, and the status code comes from the
host's exception handler, so it depends on the ASP.NET Core runtime: `UseExceptionHandler` answers
500 on ASP.NET Core 10 and earlier but preserves `BadHttpRequestException.StatusCode` (400) on
ASP.NET Core 11. Assert on the exception, not on a 500.

## Output-cache eviction

`RecordingOutputCacheStore` implements `IOutputCacheStore` for tests interested in eviction.
It records `EvictionObserved`, the last `EvictionTag`, and `EvictionCancellationToken` before
throwing an optional `EvictionException`. Reads always miss and writes do nothing; it does not
simulate a functioning cache. Create a separate instance for each independently running scenario.
