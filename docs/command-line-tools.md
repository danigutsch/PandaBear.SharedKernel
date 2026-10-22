# Command-line tool guidance

This guidance covers every tool under `tools/`.

## Packaging

- Set `PackAsTool`, a unique `ToolCommandName` without an extension, `PackageId`, and `PackageReadmeFile`, as
  described in [Create a .NET tool](https://learn.microsoft.com/dotnet/core/tools/global-tools-how-to-create).
  Command names start with `sharedkernel-`.
- Ship framework-dependent tool packages. Add
  [RID-specific, self-contained, or NativeAOT tool packages](https://learn.microsoft.com/dotnet/core/tools/rid-specific-tools)
  only when CI builds and consumes each claimed platform. NativeAOT packages build on the operating system they
  target. Include `any` for a framework-dependent fallback.
- Keep tools AOT- and trimming-compatible: `IsAotCompatible`, source-generated JSON, `[GeneratedRegex]`.
- Write the README per the [NuGet package README guidance](https://learn.microsoft.com/nuget/nuget-org/package-readme-on-nuget-org):
  what the tool does, prerequisites, installation, usage, limits, exit codes, and security notes. Images use
  allow-listed absolute URLs.

## Commands and parsing

- Build the command tree with [System.CommandLine](https://learn.microsoft.com/dotnet/standard/commandline/). It
  has no transitive packages on .NET 8 or later and supports trimming and AOT.
- Follow the [design guidance](https://learn.microsoft.com/dotnet/standard/commandline/design-guidance):
  - Names are lowercase kebab-case.
  - Leaf commands are verbs. A command with subcommands only groups them.
  - Options are nouns and take values, not actions.
  - Short aliases are rare. `-o/--output`, `-v/--verbosity`, and `-i/--interactive` keep their conventional
    meanings.
  - Required options set `Required = true`.
- Use System.CommandLine defaults for help, version, response files, POSIX bundling, and parse errors. A tool
  that changes a default explains why in a comment next to the change.
- Each action returns an `int` exit code. A command tree is either synchronous or asynchronous, never mixed.
- Parse errors use System.CommandLine's exit code and go to standard error. The README documents domain exit
  codes.
- System.CommandLine handles Ctrl+C and SIGTERM. Actions pass their `CancellationToken` into child processes
  and cleanup work. Set `InvocationConfiguration.ProcessTerminationTimeout` only when cleanup needs longer than
  the default two seconds.
- A factory builds the command tree from injected writers and environment. Tests parse and invoke the real
  command tree through it, with output captured in `InvocationConfiguration`.
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
- Overwriting files or other destructive work requires `--force` when not interactive. Offer a dry run where it
  helps.
- Disable color when `NO_COLOR` is set, when output is not a terminal, or when `TERM=dumb`.
- Print something within about 100 ms on long operations, without animations when output is not a terminal.

## Secrets

- Read secrets from a terminal prompt without echo (`Console.ReadKey(intercept: true)`), from a file, or from
  standard input. Never from arguments or environment variables, which leak through process listings, shell
  history, and child processes.
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
- Every action is reachable from the keyboard. Secret values are masked until the user reveals them.
