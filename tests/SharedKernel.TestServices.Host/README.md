# SharedKernel.TestServices.Host

The one process that runs every service the test assemblies of this repository share, once per CI run; not a package.

`sharedkernel-ci run --slice services` starts it as the slice's `service` (see `.config/sharedkernel-ci.json`). It composes each
service with the helper that the borrowing fixtures use, so the host and the fixtures cannot drift apart:

- the PostgreSQL server `postgres`, from `SharedKernel.Npgsql.Testing`;
- Umami, from `SharedKernel.Aspire.Hosting.Umami.Testing`, with its data in the database `umami` on that same server
  (`AddUmami(name, database)`) and the website `site`, which the website resource sets up in Umami when Umami is ready.

The host starts the PostgreSQL server and then waits for Umami and its website. It writes the file that
`SHAREDKERNEL_TESTING_SERVICES_READY_FILE` names all at once and readable by the current user only, one UTF-8 `NAME=value` line each:

| Variable | Value |
| --- | --- |
| `SHAREDKERNEL_TESTING_POSTGRESQL_CONNECTION_STRING` | The server connection string, with its password. |
| `SHAREDKERNEL_TESTING_UMAMI_CONNECTION_STRING` | `Endpoint={Umami's address};WebsiteId={id}`, or `Unavailable` when Umami did not start. |

A PostgreSQL server that does not start exits 1 without a ready file. Umami that does not start within five minutes, or a website
that cannot be set up, does not hold the PostgreSQL tests back: the host writes the reason to standard error and hands over
`Unavailable`, which fails every Umami test with a message that points at that report. The host prints no value it hands over.

It stops the services and exits 0 when standard input ends or fails, or SIGTERM, SIGINT or SIGHUP arrives; a second signal ends it at
once. `tests/README.md` describes how to run it by hand. Its tests are in `SharedKernel.Npgsql.Tests` (`ReadyFileTests`,
`ShutdownRequestTests`, `SharedTestServicesTests`), and the Umami hand-over in `SharedKernel.Aspire.Hosting.Umami.Tests`.

## See Also

- [tests/README.md](../README.md)
- [docs/development.md, Shared test services](../../docs/development.md#shared-test-services)
