# SharedKernel.IntegrationTesting

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

## Boundaries

- Keep app-specific resource names, data builders, and workflows in consuming test projects.
- Do not expose raw `IServiceProvider` or generic scope plumbing through these helpers.
- SharedKernel integration-testing helpers can be added for one current in-repo caller when the helper is
  a reusable library capability with a stable boundary.
- Keep helper APIs small, host-agnostic, documented, and free of app business rules.
- Keep provider-specific behavior in provider-named helpers, such as PostgreSQL reset helpers.
- Do not add Testcontainers, Respawn, generic fixture frameworks, or broader lifecycle abstractions for
  speculative future needs.
- Prefer explicit operations over hidden global setup so tests can show which external state they reset.
- Test AppHost isolation keeps DCP resource names suffixed while Aspire randomizes resource ports and disables
  the dashboard. Aspire service-discovery names remain unchanged.

## Usage

- Add a package reference to `SharedKernel.IntegrationTesting`.
- Start AppHost-backed tests through `AspireTestApplication.Start<TAppHost>(...)`; create resource-only test
  builders with `AspireTestApplication.CreateBuilder(...)` and pass them to `Start(...)`.
- `AspireTestApplication` applies DCP suffixes and preserves Aspire testing defaults; do not bypass or recreate
  this isolation in consuming fixtures.
- Dispose the returned application in the fixture `DisposeAsync` path.
- Reset PostgreSQL with `PostgreSqlPublicSchemaReset.Reset(connection, ct)` before serial tests that need
  a known database baseline.
- Pass explicit excluded table names when immutable test evidence must survive the reset.

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
