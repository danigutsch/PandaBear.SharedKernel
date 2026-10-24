# Command-line tool guidance

This guidance covers every tool under `tools/`.

## Packaging

- Packaged tools set `PackAsTool`, a unique `ToolCommandName` without an extension, `PackageId`, and
  `PackageReadmeFile`, as described in [Create a .NET tool](https://learn.microsoft.com/dotnet/core/tools/global-tools-how-to-create)
  and the [pack target](https://learn.microsoft.com/nuget/reference/msbuild-targets#pack-target). Command names
  start with `sharedkernel-`.
- Candidates pack the tools named in `SharedKernelPackagePlanner.PackagedTools`. A convention test requires every
  `PackAsTool` project there except `SharedKernel.VaultManager.Tool`, which candidates do not ship.
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

## Secrets

- Read secrets from a terminal prompt without echo (`Console.ReadKey(intercept: true)`), from a file, or from
  standard input. Never from arguments or environment variables, which leak through process listings, shell
  history, and child processes. A GitHub token is the exception: tools that call GitHub read it from `GH_TOKEN`
  or `GITHUB_TOKEN`, as [`gh`](https://cli.github.com/manual/gh_help_environment) does, and keep both out of
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
