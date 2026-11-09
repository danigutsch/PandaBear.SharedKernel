# Sandbox tool

Date: 2026-11-08. Status: accepted.

Coding agents run commands in a sandbox: on Linux a bubblewrap mount namespace, a seccomp filter, and a filtering network
proxy. Several .NET tools fail there with errors that do not say why. `dotnet format` in project mode and `dotnet test`
crash with `SocketException (13): Permission denied`, because a .NET named pipe is a Unix domain socket on Linux and macOS
and the sandbox runtime installs a rule that refuses to create any `AF_UNIX` socket. People lose time guessing at paths.
`sharedkernel-sandbox doctor` tries the operations and reports what it found.

## Decision

- The tool lives in SharedKernel and is generic. It embeds no path, host, or credential location; the consuming
  repository lists the paths that must stay unreadable in the optional `.config/sharedkernel-sandbox.json`.
- A probe tries one operation and undoes it, so nothing that lasts changes, and no probe makes a network call. The
  operations sit behind `ISandboxEnvironment`; the command logic is tested with a scripted fake and the real
  implementation with a smoke test.
- **Probe the real operation, stage by stage.** Creating a socket is only the first thing `dotnet test` and project-mode
  `dotnet format` do: they also bind it to a path in the temporary folder, connect a second socket, and exchange bytes. A
  sandbox can refuse each stage on its own (a seccomp filter on creation, Landlock rights for creating socket files and for
  connecting, separate bind and outbound rules in Seatbelt), so a creation-only probe could report "allowed" for a
  sandbox in which the tools still fail. Each probe runs all stages with a two-second limit, removes its socket file, and
  names the stage that was refused; the hint about the likely layer is inference.
- **Sockets are advisory.** A denied socket is reported with its consequence and the workarounds that keep the sandbox strict
  (`dotnet format whitespace --folder`, a build with code-style enforcement, and running the built test executable
  directly), and does not fail the run: allowing every Unix socket opens the container engine and the ssh and gpg agents to
  every command, so a sandbox may refuse them on purpose. The write probes for the temporary and NuGet folders are
  required.
- **Configured paths are not relative.** `deniedReadPaths` names places outside the repository, so it is the one
  configuration value that is absolute or starts with `~/`, an exception to the relative-path rule of the other tools; the
  tool only probes such a path and never writes there.
- **Exposure needs content.** A sandbox can hide a path behind an empty folder or an empty file, so opening or listing a
  path proves nothing. A configured path is exposed only when content can be read from it; when nothing can be read it is
  skipped, because an empty path, a hidden path, and a missing path cannot be told apart. This was found by running the first
  version, which reported hidden credential folders as exposed. For a folder, content means a file inside it: some
  sandboxes allow listing and refuse reading, and then the contents are protected. Sockets, FIFOs, and a read that does not
  answer within two seconds are skipped, because they cannot be opened like a file.
- **A misspelled root fails.** `--root` must exist, so the denied-read check cannot be skipped by a typo.

## Raise upstream

- dotnet/roslyn: a way to load a project without the out-of-process build host, which is always started now, or a clear error
  when socket creation is refused.
- dotnet/sdk and testfx: a documented way to run Microsoft.Testing.Platform without the named-pipe channel, and a clear
  error when it cannot be created.
- The sandbox runtimes: a socket-capability probe in their diagnostics, so users do not have to find this by reading stack
  traces.

## Deferred

- Generating, merging, and verifying the sandbox settings from a profile: done, see [Sandbox settings](20261109-sandbox-settings.md).
- A socket-free check lane (folder-mode whitespace, build, and direct test runs).
- Probing the network, because it makes an outbound call and needs an opt-in.
