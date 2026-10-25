# Command-line tool guidance

This guidance covers every tool under `tools/`. Tool code holds no consuming application's policy, data, or defaults;
the consuming repository supplies them, as [Consumer configuration](#consumer-configuration) describes.

## Packaging

- Packaged tools set `PackAsTool`, a unique `ToolCommandName` without an extension, `PackageId`, and
  `PackageReadmeFile`, as described in [Create a .NET tool](https://learn.microsoft.com/dotnet/core/tools/global-tools-how-to-create)
  and the [pack target](https://learn.microsoft.com/nuget/reference/msbuild-targets#pack-target). Command names
  start with `sharedkernel-`.
- Candidates pack the tools named in `SharedKernelPackagePlanner.PackagedTools`. A convention test requires every
  `PackAsTool` project in that list, except `SharedKernel.VaultManager.Tool`, which candidates do not ship.
- `SharedKernel.CI.Tool` and `SharedKernel.RepoConfig.Tool` also run from source before a candidate exists. They
  reference only System.CommandLine, no other project, and link shared sources only from `tools/Shared`. A
  convention test keeps them that way, and CI builds each from a cone sparse checkout of its directory and
  `tools/Shared` on Linux and Windows, then runs it from outside the checkout.
- Ship framework-dependent tool packages. Add
  [RID-specific, self-contained, or NativeAOT tool packages](https://learn.microsoft.com/dotnet/core/tools/rid-specific-tools)
  only when CI builds and consumes each claimed platform. NativeAOT packages build on the operating system they
  target. Include `any` for a framework-dependent fallback.
- Keep tools AOT- and trimming-compatible: `IsAotCompatible`, source-generated JSON, `[GeneratedRegex]`. A tool
  whose dependencies rule this out says why in its project file.
- Write the README per the [NuGet package README guidance](https://learn.microsoft.com/nuget/nuget-org/package-readme-on-nuget-org):
  what the tool does, prerequisites, installation, and usage. Add limits, exit codes, and security notes. Images
  use allow-listed absolute URLs.
- Installation maps the candidate feed in `NuGet.Config`: with the SDK pinned in `global.json`, `dotnet tool
  install` rejects `--add-source` when package source mapping is enabled. Local-tool examples put `--` before the
  tool's arguments, as in `dotnet tool run sharedkernel-docs -- --help`, because `dotnet tool run` answers
  `--help` itself.

## Commands and parsing

- Build the command tree with [System.CommandLine](https://learn.microsoft.com/dotnet/standard/commandline/). It
  has no transitive packages on .NET 8 or later and supports trimming and AOT.
- Follow the [design guidance](https://learn.microsoft.com/dotnet/standard/commandline/design-guidance):
  - Names are lowercase kebab-case.
  - Leaf commands are verbs. A command with subcommands only groups them.
  - Options are nouns that give the command parameters, never a different action. `--help` and `--version` are
    the exceptions.
  - Short aliases are rare. `-o/--output`, `-v/--verbosity`, and `-i/--interactive` keep their conventional
    meanings.
- Required options set [`Required = true`](https://learn.microsoft.com/dotnet/standard/commandline/syntax#required-options).
- Use System.CommandLine defaults for help, version, response files, POSIX bundling, and parse errors. A tool
  that changes a default explains why in a comment next to the change. Help names the root command after the
  assembly, such as `SharedKernel.Versioning.Tool`. An argument that starts with `@` names a
  [response file](https://learn.microsoft.com/dotnet/standard/commandline/syntax#response-files).
- Each action returns an `int` exit code. A command tree is either synchronous or asynchronous, never mixed.
- Parse errors exit with code 1, write the errors to standard error, and write help to standard output. The
  README documents every other exit code.
- Ctrl+C and SIGTERM cancel the `CancellationToken` of an asynchronous action. Actions pass it to child
  processes, kill the child process tree when it is canceled, and wait for the tree to exit. Cleanup that must
  still run uses its own bounded token.
- A canceled action exits 1. When cleanup outlives
  [`InvocationConfiguration.ProcessTerminationTimeout`](https://learn.microsoft.com/dotnet/standard/commandline/how-to-parse-and-invoke#process-termination-timeout),
  System.CommandLine exits 130 for SIGINT or 143 for SIGTERM. Raise the two-second default only when cleanup
  needs longer. A signal that System.CommandLine does not handle, such as one during start-up, ends the
  process with the same codes.
- Child processes start only from fully qualified paths. A bare command name such as `git` resolves only in the
  absolute directories of `PATH`, as `ExecutableResolver` does, never in the application directory, the working
  directory, or a relative `PATH` entry, so a checkout cannot supply the executable.
- A factory builds the command tree from injected environment, such as the working directory, a process
  runner, or a clock. Actions write to `ParseResult.InvocationConfiguration.Output` and `Error`.
- Tests parse and invoke the real tree through the factory, capture all output in one
  `InvocationConfiguration`, and set its `ProcessTerminationTimeout` to `null` so System.CommandLine registers
  no signal handlers in the test host. Caller tests parse every command line in workflows, scripts, and tool
  READMEs with the real tree. They do not read `docs/` prose, because documentation-only changes skip tests.
  Test projects keep System.CommandLine's text English with
  [`SatelliteResourceLanguages`](https://learn.microsoft.com/dotnet/core/project-sdk/msbuild-props#satelliteresourcelanguages).
- A command-line change updates every caller in the same change: CI workflows, scripts, documentation, tests,
  other tools, and consuming repositories. Consuming repositories adopt it with the tool package version that
  contains it.
- Secrets are never arguments.

## Behavior

These points follow [clig.dev](https://clig.dev/), which excludes full-screen terminal UIs; apply them to the
non-interactive commands and to how they start a UI.

- Primary and machine-readable output goes to standard output. Diagnostics, progress, and errors go to standard
  error.
- Exit with 0 on success and a documented non-zero code for each important failure mode.
- Prompt only when standard input is a terminal. Without a terminal, fail with a message that names the flag or
  input to use instead. A full-screen UI refuses to start without a terminal.
- Work that changes remote state or deletes data that cannot be restored requires `--force` when not
  interactive. A command may preview by default and act only with `--force`, which never skips validation.
- Disable color when `NO_COLOR` is set and not empty, when output is not a terminal, or when `TERM=dumb`.
  System.CommandLine colors its parse errors red when output is not redirected; tools keep that default.

## Consumer configuration

- A tool that applies consumer policy reads `<root>/.config/<tool-command>.json`, such as
  `.config/sharedkernel-repo.json`, where `--root` defaults to the current directory. No option names another
  configuration file. The exceptions are `sharedkernel-docs`, which reads the file that `--config` names;
  `sharedkernel-repo`, whose `security` commands read `.config/security-baseline.json` and whose roadmap commands
  read `roadmap/config.json`; and `sharedkernel-playwright-mcp`, which reads `PLAYWRIGHT_MCP_*` environment
  variables.
- The file is strict JSON of at most 1 MiB with `"version": 1`. Property names are camelCase and match exactly;
  comments, trailing commas, and unknown or repeated properties are errors. Read it with a source-generated
  `JsonSerializerContext` that sets `UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow` and
  `AllowDuplicateProperties = false`. Errors name the file and the JSON path of the problem.
- A command fails when the file lacks the section it needs.
- `sharedkernel-ci select`, `run`, `openapi`, `coverage`, and `run-mutation-tests` read `.config/sharedkernel-ci.json`:
  the solution, the full-validation and documentation path patterns, the always-selected test projects, whether changed
  projects need covering tests, the test slices with their OpenAPI targets, Playwright browsers, and services, the OpenAPI
  targets, the changed-code coverage threshold, source patterns, and symbol directories, and the `dotnet-stryker`
  version and Stryker config files. The
  [CI tool README](../tools/SharedKernel.CI.Tool/README.md#configuration) describes the format and the selection rules.
- `sharedkernel-repo verify-links`, `verify-gherkin`, `dependency verify-license-exceptions`, and `github sync-labels`
  read `.config/sharedkernel-repo.json`: the documentation files to scan for links, the Gherkin rules, the license
  exceptions, and the repository labels. The
  [repo tool README](../tools/SharedKernel.RepoConfig.Tool/README.md#configuration) describes the format.
- `sharedkernel-k6 run` reads `.config/sharedkernel-k6.json`: the script and results directories, the container engine
  and digest-pinned image, and the scenarios with their script, base URL variable, forwarded environment variables,
  and optional host process. The [k6 tool README](../tools/SharedKernel.K6.Tool/README.md#configuration) describes
  the format.
- Paths in the file are relative to the root and use `/`. They are never rooted, have no empty, `.`, or `..`
  segments, and resolve under the root without following symbolic links.
- Path patterns match with `FileSystemName.MatchesSimpleExpression(pattern, path, ignoreCase: false)`. Regular
  expressions from configuration compile with `RegexOptions.NonBacktracking` and a match timeout.

## GitHub Actions

- Commands write GitHub Actions outputs and step summaries only to the files that `--github-output <path>` and
  `--github-summary <path>` name. Except for the `sharedkernel-repo automation` steps, they do not read
  `GITHUB_OUTPUT` or `GITHUB_STEP_SUMMARY`.
- Workflows hand values to `run:` scripts through `env:`, never through `${{ }}` expressions inside the script.
  Pull request text reaches a command only through `--event "$GITHUB_EVENT_PATH"`.
- Workflows set a GitHub token only on the steps that call the GitHub API, as described under
  [Secrets](#secrets). Every `actions/checkout` sets `persist-credentials: false`.

## Running before a candidate is staged

A consumer job that runs before its pinned candidate is staged, such as the job that stages it, builds
`sharedkernel-ci` or `sharedkernel-repo` from SharedKernel source at the pin's `libraryRevision`:

1. Check out that revision of `danigutsch/PandaBear.SharedKernel` into a directory of its own, with a cone sparse
   checkout of `tools/<project>` and `tools/Shared`.
2. Build inside that checkout with `dotnet build tools/<project> -c Release`, so SharedKernel's `global.json`,
   `NuGet.Config`, and the tool's lock file apply.
3. Run `dotnet run --project <checkout>/tools/<project> -c Release --no-build -- <command>`.

The consumer and SharedKernel pin the same SDK version and roll-forward policy in `global.json`;
`sharedkernel-ci stage-candidate` refuses to stage a candidate when they differ. Jobs that run after staging install
the candidate's tools from that local feed and run them with `dotnet tool run`.

## Shared sources

Helpers that two or more tools call live in `tools/Shared`, one internal type per file in the `PandaBear.Tooling`
namespace. A tool compiles each file it uses as a link, such as
`<Compile Include="../Shared/ExecutableResolver.cs" Link="Shared/ExecutableResolver.cs" />`. Process runners and
GitHub HTTP clients stay in each tool.

## Secrets

- Read secrets from a terminal prompt without echo (`Console.ReadKey(intercept: true)`), from a file, or from
  standard input. Never from arguments or environment variables, which leak through process listings, shell
  history, and child processes. A GitHub token is the exception: commands that call the GitHub API read it from
  `GH_TOKEN` or `GITHUB_TOKEN`, as [`gh`](https://cli.github.com/manual/gh_help_environment) does, and keep both out of
  child process environments.
- Do not use `SecureString` ([DE0001](https://github.com/dotnet/platform-compat/blob/master/docs/DE0001.md)).
  Keep secret bytes in arrays you own and clear them with
  [`CryptographicOperations.ZeroMemory`](https://learn.microsoft.com/dotnet/api/system.security.cryptography.cryptographicoperations.zeromemory).
  Strings cannot be cleared, so keep plaintext lifetimes short.
- Create secret files with `FileMode.CreateNew` and
  [`UnixCreateMode`](https://learn.microsoft.com/dotnet/api/system.io.filestreamoptions.unixcreatemode) set to
  owner read and write. Write a temporary file and move it into place so a replaced file never keeps wider
  permissions.
- Exception messages, logs, reports, and `ToString()` output never include secret values. See the
  [OWASP Secrets Management Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Secrets_Management_Cheat_Sheet.html)
  for memory dumps and wider handling.

## Terminal UIs

- Use Terminal.Gui's instance model from its
  [application documentation](https://github.com/tui-cs/Terminal.Gui/blob/develop/docfx/docs/application.md):
  `using IApplication app = Application.Create().Init();`, `app.Run(...)`, and `Runnable<TResult>` for dialogs
  that return values. Do not use the static `Application` members.
- Read the [release notes](https://github.com/tui-cs/Terminal.Gui/releases) before upgrading; minor versions
  contain breaking changes.
- Keep UI state and behavior in plain classes without Terminal.Gui dependencies, and unit test them directly.
- Test views headlessly with [input injection](https://github.com/tui-cs/Terminal.Gui/blob/develop/docfx/docs/input-injection.md):
  `Application.Create(new VirtualTimeProvider())`, the ANSI driver, `InjectKey`, and virtual time instead of
  sleeping. Assert on state and events, not screen text.
- Headless view tests set the environment variable `DisableRealDriverIO=1` and replace the driver's clipboard with
  `FakeClipboard`. Terminal.Gui 2.5.0 otherwise attaches to the controlling terminal through `/dev/tty`, even when
  standard input and output are redirected, and uses the system clipboard.
- Every action is reachable from the keyboard. Secret values are masked until the user reveals them.

## Adding a tool

1. Create `tools/SharedKernel.<Name>.Tool` in the `PandaBear.<Name>.Tool` namespace, with the packaging properties
   above, the `sharedkernel-<name>` command name, and the `PandaBear.SharedKernel.<Name>.Tool` package ID. Add it and
   `tests/SharedKernel.<Name>.Tool.Tests` to `SharedKernel.slnx`.
2. Add the project to `SharedKernelPackagePlanner.PackagedTools`, so candidates pack it and candidate consumption
   installs it and runs `sharedkernel-<name> --help` on Linux and Windows.
3. Add System.CommandLine's `DiagramDirective` to the root command, so consumer caller tests can parse command lines
   with `[diagram]` without running them.
4. Write the packed README, and add the test project to the `windows-package-consumption` job's test step when
   consumers run the tool on Windows.
