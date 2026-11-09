# PandaBear.SharedKernel.Sandbox.Tool

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Probes what the environment it runs in allows, and says what that means for .NET tools. Coding agents run commands in a
sandbox, and several .NET tools fail there with errors that do not say why: `dotnet format` in project mode and
`dotnet test` crash with `SocketException (13): Permission denied`, because a .NET named pipe is a Unix domain socket on
Linux and macOS and the sandbox refuses to create one.

Each probe tries an operation and undoes it (the socket files it binds are removed again). Nothing that lasts is changed, and no network call is made.

## Installation

Install a validated candidate as a local tool from a trusted feed. Add the feed to `NuGet.Config`, map
`PandaBear.SharedKernel.*` to it, and map every other package, for example `*`, to the existing source.

```text
dotnet tool install PandaBear.SharedKernel.Sandbox.Tool --version <validated-candidate-version>
```

A local tool runs as `dotnet tool run sharedkernel-sandbox -- doctor`.

## Usage

```text
sharedkernel-sandbox doctor
sharedkernel-sandbox doctor --root ../app
sharedkernel-sandbox --help
```

`doctor` takes `--root <path>`, the repository root that holds the optional configuration file, and defaults to the
current directory. It prints one line per probe, and, for a bad outcome, what it means and what to do:

```text
denied unix-socket: Creating a Unix domain socket was refused (permission denied).
  -> Project-mode 'dotnet format' and 'dotnet test' cannot start ...
allowed temp-write: A file can be created there.
```

| Probe | Good outcome | Fails the run |
| --- | --- | --- |
| `unix-socket` | allowed: create, bind, connect, and exchange a byte | no, reported only |
| `named-pipe` | allowed: create, connect, and exchange a byte | no, reported only |
| `temp-write` | allowed (a folder that does not exist counts as failed) | yes |
| `nuget-write` | allowed (skipped when the folder does not exist) | yes |
| `denied-read <path>` | blocked | yes, when the path is exposed |

A denied socket is not an error: a sandbox may refuse them on purpose, and allowing every Unix socket opens the container
engine and the ssh and gpg agents to every command. The advice names the workarounds that keep the sandbox strict:
`dotnet format whitespace --folder <dir>`, a build with code-style enforcement, and running the built test executable
directly.

## Configuration

`.config/sharedkernel-sandbox.json` is optional, strict JSON with `"version": 1`:

```json
{
  "version": 1,
  "deniedReadPaths": ["~/.ssh", "~/.gnupg", "/etc/shadow"]
}
```

`deniedReadPaths` lists paths that the sandbox must not let a command read. Each is absolute or starts with `~/`. The
tool embeds no path of its own. Without the file, `doctor` skips this check and says so.

A path counts as **exposed** only when content can be read from it: the first byte of a file, or, for a folder, a file
inside it (some sandboxes allow listing a folder and refuse reading its files, and then the contents are protected; a
folder with entries but no regular file in its first 20 entries counts as exposed, since its names can be seen). A sandbox
can also hide a path behind an empty folder or an empty file, which looks the same as a path that holds nothing, so a path
with nothing readable is **skipped**, not reported as blocked. A socket, a FIFO, or any path that does not answer within two
seconds cannot be probed this way and is skipped, and `~/` is skipped when the home folder is unknown.

## Limits

- A sandbox can refuse creating a socket, binding it, and connecting to it independently of each other (a seccomp filter,
  Landlock file and connect rights, or separate bind and outbound rules in macOS Seatbelt). The probes therefore do what
  `dotnet test` and project-mode `dotnet format` do: create a socket, bind it to a path in the temporary folder, connect a
  second socket, and exchange a byte, and the message names the stage that was refused. The hint about which layer probably
  refused it is an inference, not a measurement.
- .NET works out the folder of a named pipe's socket once per process from `TMPDIR` and keeps it, so set `TMPDIR` before the
  tool starts; changing it afterwards has no effect on the pipe. A temporary folder path that is too long for a socket path
  (about 100 bytes) makes the probes report a failure. A different `TMPDIR` only helps when binding is refused by path; it
  does not help against a filter on socket creation.
- .NET cannot use Linux abstract-namespace sockets for named pipes, so they are not probed.
- `--root` must be an existing directory, so that a misspelled root cannot silently skip the denied-read check.

## Exit codes

| Code | Meaning |
| --- | --- |
| 0 | No required probe has a bad outcome. |
| 1 | A required probe is denied or failed, or a configured path is exposed. |
| 2 | The configuration file is invalid, or `--root` is not an existing directory. |
