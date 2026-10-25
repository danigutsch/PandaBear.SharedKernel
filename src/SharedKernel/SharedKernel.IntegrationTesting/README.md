# SharedKernel.IntegrationTesting

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Shared helpers for integration tests that need real hosted resources. These helpers are part of the
SharedKernel seed and should stay reusable outside this repository.

## Scope

- Aspire application startup and cleanup.
- Unique DCP resource-name suffixes for test-created Aspire AppHosts.
- Aspire testing defaults that disable the dashboard and randomize resource ports for concurrent test hosts.
- Direct container endpoints with Aspire-allocated dynamic ports; project endpoints retain Aspire proxies.
- Resource health waits with explicit timeouts and cancellation.
- A two-minute host shutdown budget aligned with DCP resource cleanup, with cleanup failures preserved.
- PostgreSQL public-schema resets for known-baseline tests.
- Start-on-first-demand sharing of an expensive test resource with `OnDemandResource<T>`.

## Boundaries

- Keep app-specific resource names, data builders, and workflows in consuming test projects.
- Do not expose raw `IServiceProvider` or generic scope plumbing through these helpers.
- SharedKernel integration-testing helpers can be added for one current in-repo caller when the helper is
  a reusable library capability with a stable boundary.
- Keep helper APIs small, host-agnostic, documented, and free of app business rules.
- Keep provider-specific behavior in provider-named helpers, such as PostgreSQL reset helpers.
- Do not add Testcontainers, Respawn, generic fixture frameworks, or broader lifecycle abstractions for
  speculative future needs. `OnDemandResource<T>` is the one lifecycle primitive: it starts a single value on
  first demand and disposes it once. It decides nothing about what a value means, which tests need it, or what
  teardown beyond disposal the value requires.
- Prefer explicit operations over hidden global setup so tests can show which external state they reset.
- Test AppHost isolation keeps DCP resource names suffixed while Aspire randomizes resource ports and disables
  the dashboard. Aspire service-discovery names remain unchanged.

## Usage

- Add a package reference to `PandaBear.SharedKernel.IntegrationTesting` at the validated private candidate version.
- Start AppHost-backed tests through `AspireTestApplication.Start<TAppHost>(...)`; create resource-only test
  builders with `AspireTestApplication.CreateBuilder(...)` and pass them to `Start(...)`.
- `AspireTestApplication` applies DCP suffixes and preserves Aspire testing defaults; do not bypass or recreate
  this isolation in consuming fixtures.
- Dispose the returned application in the fixture `DisposeAsync` path.
- Reset PostgreSQL with `PostgreSqlPublicSchemaReset.Reset(connection, ct)` before serial tests that need
  a known database baseline.
- Pass explicit excluded table names when immutable test evidence must survive the reset.

## On-demand resources

xUnit creates an assembly fixture whenever any selected test runs, including a test that never uses what the
fixture owns. Let the fixture hold an `OnDemandResource<T>` instead of starting the resource in
`InitializeAsync`, so only tests that ask for the resource pay for it.

```csharp
public sealed class ServerFixture : IAsyncLifetime
{
    private readonly OnDemandResource<AspireTestApplication> _server = new(StartServer);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async Task<string> GetConnectionString(CancellationToken ct)
    {
        var server = await _server.Get(ct);
        return await server.GetConnectionString("postgres", ct);
    }

    public ValueTask DisposeAsync() => _server.DisposeAsync();

    // Aspire finds its DCP metadata by walking the synchronous call stack, so the builder is created here, in the
    // executable test assembly, and the first caller runs this method synchronously.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Task<AspireTestApplication> StartServer(CancellationToken ct)
    {
        var builder = AspireTestApplication.CreateBuilder();
        _ = builder.AddPostgres("postgres");
        return AspireTestApplication.Start(builder, ["postgres"], resourceStartupTimeout: null, ct);
    }
}
```

- The first `Get` invokes the start delegate on the calling thread, without `Task.Run`, and never again. The
  delegate receives a token owned by the resource, so it must not depend on the first caller's test token. Other
  callers wait for the synchronous part of the start and then share the same task. The delegate must not call `Get`
  or `DisposeAsync` on its own resource: a `Get` from its synchronous part throws `InvalidOperationException`, and a
  later call would wait for the start itself and never complete.
- A cancelled `ct` throws before anything starts, and cancelling it while waiting abandons only that caller's wait;
  it never cancels or poisons the shared start.
- A failed start is cached: every caller observes the same failure and the delegate is not invoked again, so a
  multi-minute startup timeout is not repeated for every test. A synchronous throw becomes a faulted task. A start
  that is cancelled by anything but disposal, such as its own startup timeout, fails with an
  `InvalidOperationException` that carries the cancellation as its inner exception.
- `DisposeAsync` never starts the resource and is safe in any state. It does nothing for a resource that never
  started, cancels and awaits a pending start, and otherwise disposes the value once, through the optional stop
  delegate or, when there is none, through `IAsyncDisposable` (preferred) or `IDisposable`. Failures of a start
  surface from `Get`; a start that failed before disposal is reported by `DisposeAsync` only when no caller of `Get`
  received the failure. Any cancellation that a pending start shows after disposal requested one is swallowed, even
  when another source raised it. A pending start that fails with anything else, such as a failure carrying cleanup
  errors, also surfaces from `DisposeAsync`, as do failures of cancellation callbacks, of the stop delegate and of the
  value's own disposal; several failures are combined in an `AggregateException`. Overlapping and later calls wait
  for the first disposal and never repeat its failure. `Get` after disposal throws `ObjectDisposedException` without
  starting anything.
- Dispose only after every borrower has finished.

## Concurrent container ports and cleanup

Aspire 13.6 allocates dynamic proxyless container ports outside the OS ephemeral range. Test applications
use those direct endpoints so Podman cannot assign another container a port already reserved as a DCP
proxy target. Do not add fixed host ports or custom port leases to consuming fixtures. Project endpoints
retain their normal proxy behavior. This policy applies to resource-only and AppHost-backed starts.

The host and helper both allow two minutes for teardown, matching DCP's default cleanup budget. The
previous 30-second host deadline could cancel valid cleanup before the orchestrator finished. Cleanup
still awaits all phases and reports failures; no resource-cleanup wait is disabled.

References: [Aspire networking](https://aspire.dev/fundamentals/networking-overview/) and
[DCP environment settings](https://github.com/microsoft/dcp/blob/main/README.md#environment-variables-affecting-dcp-behavior).

## Package identity and availability

Package ID: `PandaBear.SharedKernel.IntegrationTesting`. C# namespace: `PandaBear.IntegrationTesting`. Assembly/project identity: `SharedKernel.IntegrationTesting`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
