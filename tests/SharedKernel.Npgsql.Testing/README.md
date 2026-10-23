# PostgreSQL test support

Non-packable support shared by `SharedKernel.Npgsql.Tests` and
`SharedKernel.EventSourcing.Npgsql.Tests`. Both register a local `PostgreSqlTestServerFixture` as a native
xUnit assembly fixture, deriving from the shared PostgreSQL fixture. Each test assembly owns its own
PostgreSQL server; tests lease uniquely named databases so parallel classes cannot reset each other's state.

Each local fixture creates the builder in a method marked `NoInlining` because Aspire discovers
the executable test project's generated DCP metadata from the call stack. Keeping that method local
preserves host ownership during shared async initialization, including Release builds.
The shared fixture composes the existing `AspireTestApplication` lifecycle. Database leases retain bounded,
cancellation-independent cleanup, forced connection termination, fixture-owned name validation,
and aggregation of operation and cleanup failures. Failed lease cleanup remains registered for
assembly teardown. `PostgreSqlDatabaseTestBase` shares per-test database leasing and disposal and exposes protected
`DataSource` and `ConnectionString` properties. Consumer tests retain domain-specific setup.

Dependencies are the repository's existing Aspire PostgreSQL, Npgsql, and xUnit core versions.
This project introduces no package publication or generic host/scenario framework. Keep application
authentication, browser startup, and reset policies in their owning tests. Traits and theory data
continue to use native xUnit attributes and typed data; no custom discovery framework is needed.
