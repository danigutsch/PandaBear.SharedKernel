# Library tests

Use the repository-pinned SDK and Microsoft.Testing.Platform runner. After the documented locked restore and Release build, run `dotnet test --solution SharedKernel.slnx -c Release --no-build --no-restore`. Run a single project with `dotnet test --project <path-to-csproj> -c Release --no-build --no-restore`; narrow local debugging with `--filter-class`, `--filter-method`, `--filter-namespace`, or `--filter-trait`. CI selects complete projects and nightly runs cover the full solution.

Provider integration tests require a working local container runtime. Preserve meaningful cancellation, persistence, cleanup, privacy, and boundary regressions. Do not cap test-module concurrency or mask cleanup errors. Analyzer/generator tests exercise compilations and emitted contracts; package consumers validate installed immutable artifacts separately from project-reference tests.

Coverage uses the existing `coverlet.MTP` extension; collection and its limits are documented in the root README. No hosted coverage percentage or service is implied. The two explicitly packaged test-support projects have their own package README files; ordinary test projects remain non-packable.

## Test taxonomy

The [test inventory](../.config/test-taxonomy.json) declares the owner and expected test types for tracked source and
tool projects. Each test suite declares the projects and seams it exercises and the applicable CI slice.
The [coverage policy](../docs/TEST_GUIDELINES.md) explains how to classify analyzer, generator, code-fix, package,
and provider tests. Use `<SUT>.Tests.<Type>` for new suites and record the reason when a legacy or mixed suite
needs a migration exception.

Run the same advisory check as repository quality after the Release build.

```sh
dotnet run --project tools/SharedKernel.RepoConfig.Tool -c Release --no-build --no-restore -- verify-test-taxonomy --report
```

Stage new files first so the Git index includes them. The [command reference](../tools/SharedKernel.RepoConfig.Tool/README.md#test-taxonomy)
describes the schema and strict validation mode.

`SharedKernel.ContentRights.Tests.Unit` verifies licence declarations, attribution validation and conversion of
reviewed image credits in process. The SEO and Blazor Site suites exercise their metadata and UI projections.

`SharedKernel.CommandLine.Tests.Unit` parses the shared `--root`, `--repository`, and `--forgejo` options in process. Each
tool's own suite runs a command with `--root ""` to prove that the tool uses them.

`SharedKernel.Blazor.Site.Editing.Tests.Unit` renders the theme editor with bUnit in process: its controls, their binding, their
field paths and where a save's rule errors land.

## Shared test services

`SharedKernel.Npgsql.Tests`, `SharedKernel.EventSourcing.Npgsql.Tests` and `SharedKernel.EntityFrameworkCore.Tests` need PostgreSQL;
`SharedKernel.Aspire.Hosting.Umami.Tests` needs Umami with a website. Their fixtures start nothing until a test needs the service, and
then borrow it or start one of their own (see `SharedKernel.Npgsql.Testing/README.md` and
`SharedKernel.Aspire.Hosting.Umami.Testing/README.md`). The assemblies stay separate and run in parallel.

- CI runs them together in the `services` slice (`dotnet ci run --slice services`). The tool starts the host
  `SharedKernel.TestServices.Host` once, waits for its ready file, runs the tests with the PostgreSQL server connection string in
  `SHAREDKERNEL_TESTING_POSTGRESQL_CONNECTION_STRING` and the Umami website connection string in
  `SHAREDKERNEL_TESTING_UMAMI_CONNECTION_STRING`, and stops the host afterwards, so a complete run creates one PostgreSQL container
  and one Umami container, which keeps its data in a database on that server.
- A plain `dotnet test --solution` or `dotnet test --project` starts no host. Each assembly that runs a test that needs a service
  starts one of its own on first demand, once for the assembly, so a complete run creates up to one PostgreSQL container per
  PostgreSQL assembly, and one Umami container with a PostgreSQL container of its own for the Umami assembly; a run that selects no
  such test creates none.
- To run assemblies against one instance of each service by hand, follow what the tool does: start `dotnet
  SharedKernel.TestServices.Host.dll` from the Release build with standard input from a pipe that you keep open and
  `SHAREDKERNEL_TESTING_SERVICES_READY_FILE` set to a new file path in a private directory; wait for that file, which holds one
  `NAME=value` line for each of the two variables; export each variable with the rest of its line as its value for the test run
  only; then close the pipe, or send SIGTERM, and wait for the host to exit, which removes the containers. A second signal ends the
  host at once, and so does a kill; Aspire's DCP process runs detached and then removes the containers within seconds, so that is the
  way out of a stop that hangs. While Aspire checks for a container runtime the host does not look at stop requests, which takes
  about a minute when there is none. The host prints no value it hands over and no password. It exits 1 without a ready file when the
  PostgreSQL server does not start; when Umami does not start, it writes why to standard error and hands over `Unavailable` for
  Umami, so only the Umami tests fail. The borrowed server must be one that Aspire started: `AspireTestApplicationTests` asserts that
  it listens on a port in DCP's range, so a PostgreSQL on port 5432 fails that test. Aspire hands the generated password to the
  container runtime as an `-e` argument, so other local users of a shared machine can read it in the process list for about a
  second while the container is created.
