# Socket-free check lane

Date: 2026-11-09. Status: accepted.

In a sandbox that refuses to create Unix domain sockets, `dotnet test` (the Microsoft.Testing.Platform pipe) and project-mode
`dotnet format` (the Roslyn build host pipe) crash with `SocketException (13)`. The [sandbox tool](20261108-sandbox-tool.md)
diagnoses that, and deferred a lane that still checks a change there. Three things work without a socket: `dotnet format
whitespace --folder`, a restore and build with node reuse off, and a built Microsoft.Testing.Platform test executable run
directly.

## Decision

- **A new command, `sharedkernel-ci local-check`.** The alternatives were `run --socket-free` and a tool of its own.
  `run` reads a plan that `select` wrote for another job, builds one solution filter, starts services, collects coverage,
  and writes evidence for CI; its core is `dotnet test`, so a flag would change almost every phase and make the common
  path harder to read. A new tool would copy selection, the process runner, and the configuration. A command reuses
  selection, solution filters, the configuration, and the process abstractions, and gives the lane its own report and
  exit rules.
- **No new configuration.** `.config/sharedkernel-ci.json` keeps its strict schema. The lane uses `solution`, `selection`,
  and the slices: a test project of a slice with a `service`, `openApi` targets, or `playwright` browsers is not run,
  because only `run` provides what it needs.
- **Selection includes the working tree.** `select` compares committed history, but an agent has uncommitted work.
  `ProjectSelectionPlan.Write` gets an opt-in `includeWorkingTree` that adds `git diff HEAD` and the untracked, not
  ignored files; `select` is unchanged. `--base` is any revision, resolved to a commit, because an agent has
  `origin/main` and not an object ID at hand.
- **Lanes, not a pipeline.** Whitespace, build, and tests are reported one by one, and a failed lane does not hide the
  others, except that the tests need the build. Each is `passed`, `FAILED`, or `SKIPPED` with its reason.
- **Green must not read as complete.** The report always lists what the lane does not check: project-mode style and
  analyzer rules (CI also reports IDE0002 and others that the build does not enforce), coverage collection, and
  `dotnet test`. Skipped lanes and selected test projects that were not run are listed with the reason; `--strict` turns
  them into a failure for CI. A change that selects no project, a checkout that is not a Git work tree, and a `--base`
  that is not a commit fail, so the command cannot pass having checked nothing.
- **A refused socket is a failure with a pointer.** Output with a `SocketException`, or `Permission denied` next to a
  socket or pipe, is reported as a sandbox that blocks sockets, with `sharedkernel-sandbox doctor` as the next step. It
  is a failure, not a skip: the lane ran and did not finish. The CI tool does not depend on the sandbox tool project; the
  command name in the message is text.
- **Same process rules.** Child processes start through the tool's runner: no shell, an absolute-path executable, the
  process tree killed on cancellation. The environment sets `MSBUILDDISABLENODEREUSE=1`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`,
  and `DOTNET_CLI_TELEMETRY_OPTOUT=1`. Output in the report is the last 20 lines, shortened and escaped, and the clock is
  injected.

## Consequences

- An agent can check whitespace, compile warnings that fail the build, and the tests of the changed projects without a
  socket, and sees what is left for CI.
- The test executable is found through `dotnet msbuild -getProperty:TargetPath`, which assumes a single target framework
  per test project; a project with several needs a property that selects one.
- The tests run without `dotnet test`, so options that only the `dotnet test` front end adds are unavailable; arguments
  after `--` go to the executable.

## Deferred

- Fixing whitespace in place (`dotnet format whitespace --folder` without `--verify-no-changes`); the report prints the
  command.
- A `--tfm` option for test projects with several target frameworks.
