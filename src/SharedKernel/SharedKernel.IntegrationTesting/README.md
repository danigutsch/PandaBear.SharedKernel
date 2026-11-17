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
- Resource health and state waits with explicit timeouts and cancellation, and resource lookups on the running application model.
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
- Do not add Testcontainers, Respawn or broader lifecycle abstractions for speculative future needs.
  `OnDemandResource<T>` is the lifecycle primitive: it starts a single value on first demand and disposes it once;
  `OnDemandResources<T>` keys it by name, and `AspireSystemFixture` uses it for one application per profile. They
  decide nothing about what a value means, which tests need it, or what teardown beyond disposal it requires.
- Prefer explicit operations over hidden global setup so tests can show which external state they reset.
- Test AppHost isolation keeps DCP resource names suffixed while Aspire randomizes resource ports and disables
  the dashboard. Aspire service-discovery names remain unchanged.

## Usage

- Add a package reference to `PandaBear.SharedKernel.IntegrationTesting` at the validated private candidate version.
- Start AppHost-backed tests through `AspireTestApplication.Start<TAppHost>(...)`; create resource-only test
  builders with `AspireTestApplication.CreateBuilder(...)` and pass them to `Start(...)`.
- `AspireTestApplication` applies DCP suffixes and preserves Aspire testing defaults; do not bypass or recreate
  this isolation in consuming fixtures. Every builder it creates (`CreateBuilder`, `Start<TAppHost>`) gets a suffix of its own unless
  the arguments name one (`--DcpPublisher:ResourceNameSuffix={stack}-{run}`), which is kept.
- Pass AppHost configuration values (connection strings of servers the tests own, a data folder) as a dictionary:
  `CreateBuilder(configuration, args)`, `Start<TAppHost>(resources, timeout, args, configuration, ct)`, or `AppHostLaunch.Configuration`
  for `AspireAppHostFixture`. The AppHost reads them before its own code runs; they win over its appsettings, user secrets and
  environment variables, and arguments win over them. A `null` value contributes nothing; a key that is empty, white-space or
  contains `=` cannot travel as an argument and is rejected with `ArgumentException`.
- Every project resource the application starts gets `DOTNET_hostBuilder__reloadConfigOnChange=false`: tests never reload configuration,
  and one appsettings watcher per file and host exhausts the per-user inotify limit when many hosts run in parallel
  (`IOException: The configured user limit (128) on the number of inotify instances has been reached`).
- Started from an xUnit test or fixture, the application logs, including the log lines Aspire forwards from every resource, to the
  test output instead of the console: `TestOutputLoggerProvider` (SharedKernel.Testing.Xunit) replaces the console logger. An entry
  goes to the test running where it is logged, else to the test that started the application while that test runs, else to xUnit's
  diagnostic messages (`--xunit-diagnostics on`). `dotnet test` (.NET 11 SDK) streams what a test process writes to its console for
  every test, so a console logger would print every resource's log of every run. Started outside xUnit, such as by a process that
  serves the services tests borrow, it keeps the console. A resource that is not healthy in time still reports its last log lines.
- Dispose the returned application in the fixture `DisposeAsync` path.
- Reset PostgreSQL with `PostgreSqlPublicSchemaReset.Reset(connection, ct)` before serial tests that need
  a known database baseline.
- Pass explicit excluded table names when immutable test evidence must survive the reset.

## System tests of an AppHost

`AspireAppHostFixture<TAppHost>` is an xUnit assembly fixture that starts an AppHost on the first test that asks, once per profile and
run, each profile in its own folder of a private `TestWorkspace` that is removed afterwards. The subclass names the AppHost's settings and
resources; everything else is here:

```csharp
public sealed class SystemFixture : AspireAppHostFixture<Projects.MyApp_AppHost>
{
    protected override string WorkspaceName => "myapp-system";

    protected override AppHostLaunch Launch(string profile, string folder) =>
        new([$"--AppHost:DataFolder={folder}"], [Resources.Web, Resources.Api], TimeSpan.FromMinutes(3));
}

// In a test: var app = await fixture.Application(ct); var web = app.GetEndpoint(Resources.Web, "http");
```

- `Application(profile, ct)` starts a profile (or the `default` one) on demand and shares it; profiles keep tests that change global
  state (an import, a reset) away from tests that share one application. `AspireSystemFixture` is the base for applications that are not
  an AppHost project: implement `Start(profile, folder, ct)` and build the builder there, in the test assembly.
- `AspireTestApplication` follows every resource's log lines from the start. A health wait that runs out of time (`Start`,
  `WaitForHealthy`, `StartResource`) throws `TimeoutException` naming each pending resource with its state, health and last log lines;
  `GetResourceLogs(name)` returns them for a failure message.
- `StartResource(name, timeout, ct)` starts a resource added with `WithExplicitStart()` and waits for it: with `OnDemandResources<T>`,
  an infrastructure application can start each container on the first test that needs it.
- `Eventually.Succeeds(assertion, timeout, ct)` retries an assertion (every 250 ms by default) until it passes; on timeout the last
  failure is the inner exception. Use it instead of sleeping for state another process reaches later.
- `Eventually.Until(probe, timeout, ct)` polls a probe until it returns a non-null value and returns it; `null` means "not yet", and an
  exception from the probe ends the wait at once. On timeout it throws `TimeoutException` with the attempt count.
- `TestWorkspace.Create(name)` makes a private temporary folder; `Folder(path)` creates folders inside it, and disposal removes it,
  retrying while a stopped process still holds a file. `TestWorkspace.Create(name, releaseFiles)` runs `releaseFiles` once before the
  delete; for SQLite files in the folder pass `SqliteConnection.ClearAllPools`, because pooled connections keep the files open and Windows
  refuses to delete an open file.

## Resources of a running application

A started `AspireTestApplication` answers for the resources of its application model by name, without handing out the
`DistributedApplication`:

```csharp
var app = await fixture.Application("analytics-off", ct);
app.HasResource("analytics").ShouldBeFalse();

var website = await app.WaitForState("website", KnownResourceStates.Running, TimeSpan.FromMinutes(3), ct);
var connectionString = await app.GetConnectionString("website", ct);
```

- `HasResource(name)` tells whether the model has a resource, started or not, so a test of a setting that leaves resources out
  asserts that they are not there. Names are compared as Aspire compares them, ignoring case; `StartResource` and `WaitForState`
  find resources the same way.
- `WaitForState(name, state, timeout, ct)` waits until a resource reaches a state of `KnownResourceStates`, healthy or not, and returns
  at once when it is in that state already. Use it where the state is what the test needs, such as a resource without a health check
  that is `Running`; use `WaitForHealthy` to wait for health. It returns the resource's `ResourceEvent`, whose snapshot holds the state,
  health, URLs and properties. A resource the model does not have throws `InvalidOperationException` at once; a wait that runs out of
  time throws `TimeoutException` with the resource's state and last log lines.
- `GetConnectionString(name, ct)` reads a resource's connection string. A resource the model does not have, or one without a
  connection string, throws `ArgumentException`.

## Reading the application model

`AppHostModel` tests the AppHost's wiring without starting anything:

```csharp
await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.MyApp_AppHost>(ct);
var web = AppHostModel.Project(builder, "web");

AppHostModel.WaitedFor(web).ShouldBe(["database"]);
AppHostModel.HasHttpHealthCheck(web, "/health").ShouldBeTrue();
var environment = await AppHostModel.EvaluateEnvironment(web, ct);
AppHostModel.ConnectionResource(environment["ConnectionStrings__Default"]).Name.ShouldBe("database");
```

`EvaluateEnvironment` runs the environment callbacks as a publish would, so values stay model objects (a connection-string reference,
an endpoint reference, a plain string) instead of addresses that exist only at run time.

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
  started, cancels and awaits a pending start, and otherwise disposes the value once, through `IAsyncDisposable`
  (preferred) or `IDisposable`. Failures of a start surface from `Get`; a start that failed before disposal is
  reported by `DisposeAsync` only when no caller of `Get` received the failure. Any cancellation that a pending
  start shows after disposal requested one is swallowed, even when another source raised it. A pending start that
  fails with anything else, such as a failure carrying cleanup errors, also surfaces from `DisposeAsync`, as do
  failures of cancellation callbacks and of the value's own disposal; several failures are combined in an
  `AggregateException`. Overlapping and later calls wait for the first disposal and never repeat its failure. `Get`
  after disposal throws `ObjectDisposedException` without starting anything.
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
