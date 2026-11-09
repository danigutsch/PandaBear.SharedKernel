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
sharedkernel-sandbox settings plan
sharedkernel-sandbox settings apply --force
sharedkernel-sandbox settings verify
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

## Settings

`settings plan`, `settings apply`, and `settings verify` generate, merge, and check the `sandbox` section (and the `env`
section) of a coding agent's settings file, in the Claude Code format (`.claude/settings.json` or
`.claude/settings.local.json`), from the optional `settings` section of `.config/sharedkernel-sandbox.json`:

```json
{
  "version": 1,
  "deniedReadPaths": ["~/.ssh", "~/.gnupg"],
  "settings": {
    "profile": "dotnet",
    "target": ".claude/settings.json",
    "writeNuGetFolders": true,
    "allowWrite": ["~/.cache/tool"],
    "denyWrite": [],
    "denyRead": [],
    "allowedDomains": ["example.com"]
  }
}
```

| Property | Meaning |
| --- | --- |
| `profile` | `dotnet`, or omitted for no profile. |
| `target` | The settings file, relative to the root, a `.json` file; default `.claude/settings.json`. |
| `allowWrite`, `denyWrite`, `denyRead` | Entries of `sandbox.filesystem.*`: absolute, or starting with `~/` or `./`, without `..`. `allowWrite` refuses the root, the home folder, and any spelling of them (`/.`, `~/.`, `//`). |
| `allowedDomains` | Entries of `sandbox.network.allowedDomains`: a host name, optionally `*.`-prefixed and with a `:port` suffix. A wildcard needs two labels below it; `*.com` and `*.co.uk` are refused (a heuristic, not the public suffix list). |
| `writeNuGetFolders`, `writeDotnetHome`, `allowSdkDownload`, `environment` | Profile `dotnet` options, see below. |
| `excludedCommands`, `removeDenyRead`, `allowAllUnixSockets` | Widen the sandbox; refused unless a risk flag is passed, see below. |

`deniedReadPaths` is always added to `sandbox.filesystem.denyRead`, so the paths the sandbox must hide are in the settings.

**Profile `dotnet`** adds `api.nuget.org`, the one host a plain restore from nuget.org needs, and, unless
`"environment": false`, the `env` entries `MSBUILDDISABLENODEREUSE=1`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`, and
`DOTNET_CLI_TELEMETRY_OPTOUT=1`, which keep build servers from outliving a sandboxed command. Opt-ins, each off by
default: `writeNuGetFolders` (write access to `~/.nuget/packages` and `~/.local/share/NuGet`), `writeDotnetHome` (write
access to `~/.dotnet`), and `allowSdkDownload` (the host `builds.dotnet.microsoft.com`).

**Merge, never replace.** The tool only appends missing entries. Every other key, every entry a person added by hand, and
the order of keys and entries stay as they are. It never removes an entry because the configuration no longer lists it,
and it does not record which entries it added: a record could go stale, and removal is the operation that once overwrote a
hand-written `allowWrite`. The one removal, `removeDenyRead`, takes out exactly the entries it lists. An existing `env`
value that differs from the profile is kept and reported as a warning.

**Refused unless accepted.** `apply --force` exits 2 with a message that names the exposure unless the flag is passed. `plan`, `verify`, and `apply` without `--force` write nothing, so they do not refuse: they report as if the flag were passed and print `warning: would require --accept-risk-...`.

| Request | Flag | Exposure |
| --- | --- | --- |
| `allowAllUnixSockets: true` | `--accept-risk-allow-all-unix-sockets` | Every command can reach the container engine socket, the ssh and gpg agents, and the user session bus. |
| `excludedCommands` entries | `--accept-risk-excluded-commands` | Those commands run outside the sandbox with full access. |
| `removeDenyRead` entries | `--accept-risk-remove-deny-read` | Hidden paths, such as credential folders, become readable. |
| `allowWrite` of the home folder, any folder above it, or a path at most one level below `/` (`/home`, `/etc`, `/usr`) | `--accept-risk-broad-write` | Commands can rewrite shell startup files, ssh and gpg configuration, and other tools' state. |
| `allowWrite` of `~/.bashrc`, `~/.bash_profile`, `~/.bash_login`, `~/.bash_logout`, `~/.profile`, `~/.zshrc`, `~/.zshenv`, `~/.zprofile`, `~/.ssh`, `~/.gnupg`, `~/.config/autostart`, a folder above one of these (such as `~/.config`), a path below them, or `.git` itself, `.git/hooks`, or `.git/config` | `--accept-risk-sensitive-write` | Code planted there runs later, outside the sandbox. |

The home folder is taken from the environment (`$HOME`) and from the account database (`/etc/passwd`), and both the entry and the home folders are compared after dropping `.` segments and resolving symbolic links, so `HOME=/x`, `/home/<user>/.`, or a link to the home folder do not slip through. Other paths in `allowWrite` are not judged.

`plan` prints the edits and never writes. `apply` prints them too, and writes only with `--force`: it replaces the file
atomically (a temporary file in the same folder, renamed over the original), only when the file still has the content
that was planned, and a failure leaves the original and removes the temporary file. `verify` reports the managed entries
that the file lacks and exits 1 on drift; entries it does not manage are never reported. A second `apply` after a
successful one has nothing to change.

```text
sharedkernel-sandbox settings apply --force --root ../app
sharedkernel-sandbox settings plan --accept-risk-excluded-commands
```

Limits: the file is read as strict JSON of at most 1 MiB (no comments, trailing commas, or repeated properties) and is
written with the indentation and line endings it already has, or two spaces and LF for a new file. Rewriting goes through
`System.Text.Json`, so inline arrays are expanded to one entry per line, string escapes are normalized (the meaning is
unchanged), and a byte order mark is dropped. A file with nothing to change is not rewritten. The target must stay inside
the root and is never reached through a symbolic link. The tool writes only the keys listed here; the key names and the
path prefixes follow the Claude Code settings reference, and keys it does not know are left alone.

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
| 1 | A required probe is denied or failed, or a configured path is exposed. `settings verify`: a managed entry is missing. |
| 2 | The configuration file is invalid, `--root` is not an existing directory, or (settings) the settings file is invalid, a risk flag is missing, or a write failed. |
