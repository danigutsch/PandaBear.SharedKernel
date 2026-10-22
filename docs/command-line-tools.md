# Command-line tool guidance

This guidance covers new tools under `tools/`. It follows upstream sources, which were checked on 2026-10-22.
Older tools in this repository parse arguments by hand. That is not the pattern for new tools.

## Packaging

- Set `PackAsTool`, a unique `ToolCommandName` without an extension, `PackageId`, and `PackageReadmeFile`, as
  described in [Create a .NET tool](https://learn.microsoft.com/dotnet/core/tools/global-tools-how-to-create).
  Command names in this repository start with `sharedkernel-`.
- Ship framework-dependent tool packages by default.
- .NET 10 supports [RID-specific, self-contained, and NativeAOT tool packages](https://learn.microsoft.com/dotnet/core/tools/rid-specific-tools),
  set up through `RuntimeIdentifiers` or `ToolPackageRuntimeIdentifiers`. They produce a pointer package plus one
  package per runtime identifier. NativeAOT packages must be built on the operating system they target. Add
  them only when CI builds and consumes each claimed platform. Include `any` for a framework-dependent fallback.
- Keep the tool AOT- and trimming-compatible anyway: use `IsAotCompatible`, source-generated JSON, and
  `[GeneratedRegex]`.
- Follow the [NuGet package README guidance](https://learn.microsoft.com/nuget/nuget-org/package-readme-on-nuget-org).
  The README states what the tool does, its prerequisites, how to install and run it, its limits, and its
  security notes. Images must use allow-listed absolute URLs.

## Parsing and command design

- Use [System.CommandLine](https://learn.microsoft.com/dotnet/standard/commandline/), Microsoft's recommended
  parser. Version 2.0 is stable, has no transitive packages on .NET 8 or later, and supports trimming and AOT.
  It provides `--help`, `--version`, response files, POSIX syntax, and parse errors with a non-zero exit code.
- Follow the [design guidance](https://learn.microsoft.com/dotnet/standard/commandline/design-guidance):
  - Names are lowercase kebab-case.
  - Leaf commands are verbs. A command with subcommands only groups them.
  - Options are nouns and take parameters, not actions.
  - Use few short aliases. `-o/--output`, `-v/--verbosity`, and `-i/--interactive` keep their conventional
    meanings.
- Mark required options with `Required = true`. Return an `int` exit code from each action. Do not mix
  synchronous and asynchronous actions in one command tree.
- Response files expand `@path` arguments. Never accept secrets as arguments, even through response files.
- Test command behavior by parsing and invoking the real `RootCommand` with output captured in
  `InvocationConfiguration`, and keep the domain logic testable without the parser.

## Behavior

These points follow [clig.dev](https://clig.dev/). It excludes full-screen terminal UIs, so apply it to the
non-interactive commands and to how they start the UI.

- Write primary and machine-readable output to standard output. Write diagnostics, progress, and errors to
  standard error.
- Exit with 0 on success and a documented non-zero code for each important failure mode.
- Prompt only when standard input is a terminal. Without a terminal, fail with a message that names the flag
  or input to use instead. Refuse to start a full-screen UI without a terminal.
- Require `--force` before overwriting files or doing other destructive work non-interactively. Offer a dry run
  where it helps.
- Disable color when `NO_COLOR` is set, when output is not a terminal, or when `TERM=dumb`.
- Start printing something within about 100 ms on long operations, without animations when output is not a
  terminal.

## Secrets

- Read secrets from a terminal prompt without echo (`Console.ReadKey(intercept: true)`), from a file, or from
  standard input. Never read them from arguments or environment variables, which can leak through process
  listings, shell history, or child processes.
- Do not use `SecureString` ([DE0001](https://github.com/dotnet/platform-compat/blob/master/docs/DE0001.md)).
  Keep secret bytes in arrays you own and clear them with
  [`CryptographicOperations.ZeroMemory`](https://learn.microsoft.com/dotnet/api/system.security.cryptography.cryptographicoperations.zeromemory)
  when you are done. Strings cannot be cleared, so keep plaintext lifetimes short.
- Create secret files with `FileMode.CreateNew` and
  [`UnixCreateMode`](https://learn.microsoft.com/dotnet/api/system.io.filestreamoptions.unixcreatemode) set to
  owner read and write. Write a temporary file and move it into place so an overwritten file never keeps wider
  permissions.
- Exception messages, logs, reports, and `ToString()` output never include secret values. The
  [OWASP Secrets Management Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Secrets_Management_Cheat_Sheet.html)
  covers wider handling, including memory dumps.

## Terminal UIs

- Use Terminal.Gui v2's instance model, as described in the
  [application documentation](https://github.com/tui-cs/Terminal.Gui/blob/develop/docfx/docs/application.md):
  - Create the application with `using IApplication app = Application.Create().Init();`.
  - Run views with `app.Run(...)`, and derive dialogs that return values from `Runnable<TResult>`.
  - The static `Application` members are obsolete.
- Read the [release notes](https://github.com/tui-cs/Terminal.Gui/releases) before every upgrade, because minor
  versions still contain breaking changes. Version 2.5.0, for example, removed `ConfigurationManager`.
- Keep UI state and behavior in plain classes that do not depend on Terminal.Gui, and unit test them directly.
- Test views headlessly with [input injection](https://github.com/tui-cs/Terminal.Gui/blob/develop/docfx/docs/input-injection.md),
  and assert on state and events rather than screen text:
  - Create the application with `Application.Create(new VirtualTimeProvider())`.
  - Initialize it with the ANSI driver.
  - Send input with `InjectKey`.
  - Advance virtual time instead of sleeping.
- Every action must be reachable from the keyboard. Mask secret values by default and reveal them only on an
  explicit action.
