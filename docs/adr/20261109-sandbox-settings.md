# Sandbox settings

Date: 2026-11-09. Status: accepted.

A coding agent's sandbox is configured in its settings file, and a config manager once overwrote `sandbox.filesystem.allowWrite`
wholesale, dropping entries that people had added by hand. `sharedkernel-sandbox settings plan|apply|verify` generates the
`sandbox` section from a profile plus the consumer's `.config/sharedkernel-sandbox.json`, and merges it into the existing file
without taking anything away.

## Verified schema

Read from the Claude Code documentation on 2026-11-09 (code.claude.com/docs/en/sandboxing and settings-reference). The tool
writes only these keys:

- `sandbox.filesystem.allowWrite`, `denyWrite`, `denyRead`: lists of paths. A path is absolute (`/`), relative to the home
  folder (`~/`), or relative to the project root (`./` or no prefix) in project settings. A trailing slash is ignored by Claude
  Code, so the tool treats `~/.aws` and `~/.aws/` as the same entry. Lists merge across settings scopes.
- `sandbox.network.allowedDomains`: a list of hosts; wildcards such as `*.example.com` and a `:port` suffix are allowed.
- `sandbox.network.allowAllUnixSockets`: a boolean that opens every Unix socket; on Linux and WSL2 it is the only way to permit
  them.
- `sandbox.excludedCommands`: a list of `Bash(...)`-style command patterns that run outside the sandbox.
- `env`: "Set environment variables for every session and its subprocesses" (an object of strings; the index row is verified,
  the section body was not read).

Not written, though they exist: `enabled`, `autoAllowBashIfSandboxed`, `allowUnsandboxedCommands`, `filesystem.allowRead`,
`network.allowUnixSockets` (macOS only), `allowLocalBinding`, `enableWeakerNestedSandbox`, and the credentials keys.

Unverified, kept out of the tool: the exact semantics of `./` for a user-level file (documented as relative to `~/.claude`, so
a `./` entry in a user file means something else than in a project file), and whether an `env` entry in a project file
overrides one in the shell. `api.nuget.org` as the only host a plain restore needs was observed (service index,
registrations, vulnerability feed, and a package download answered from it without redirect), not documented;
`builds.dotnet.microsoft.com` for SDK downloads is from the install-script documentation and not exercised.

## Decision

- **Additive merge; no ownership record.** The tool appends the entries it is asked for and leaves everything else, including
  unknown keys and hand-added entries. Recording what it manages in a sidecar list would allow removing stale entries, but
  the list can go stale or be edited, and a wrong removal is exactly the drift bug this fixes; the key `managedBy` cannot
  live in the settings file itself. The cost: an entry dropped from the configuration stays in the settings file until a
  person removes it. `verify` therefore checks presence of what is configured, and absence of nothing except the `removeDenyRead` entries, which count as drift while they are still present.
- **The one removal is explicit.** `removeDenyRead` removes exactly the listed entries, needs a flag that states the risk, and
  cannot name a path that `deniedReadPaths` or `denyRead` requires.
- **Refusals name the exposure, and gate only writing.** `allowAllUnixSockets: true` (container engine, ssh and gpg agents, user bus),
  `excludedCommands` (those commands run with full access), `removeDenyRead`, `allowWrite` of the home folder, a folder above it
  or a top-level system folder (`--accept-risk-broad-write`), and `allowWrite` of shell startup files, `.ssh`, `.gnupg`,
  `.config/autostart`, or `.git` hooks and configuration (`--accept-risk-sensitive-write`) are refused with exit 2 by
  `apply --force` unless the flag that names the risk is passed (`--accept-risk-allow-all-unix-sockets`,
  `--accept-risk-excluded-commands`, `--accept-risk-remove-deny-read`). `plan`, `verify`, and a preview `apply` have nothing to
  protect: they compute as if accepted and print `warning: would require --accept-risk-...`. Home folders are taken from the
  environment abstraction (`$HOME`) and from the account database (`/etc/passwd`), and entries and homes are compared after
  resolving symbolic links, so `HOME=/x` or a link cannot hide the real home. `allowWrite` of `/` or `~/` stays a configuration
  error with no flag. An `allowAllUnixSockets: true` that a person already wrote is left alone and reported as a warning, and a
  non-boolean value is never overwritten.
- **Opt-in widening in the profile.** The `dotnet` profile adds only `api.nuget.org` and the `env` entries by default; the
  NuGet folders, `~/.dotnet`, and the SDK download host are separate opt-ins. An existing `env` value that differs is kept and
  warned about, not overwritten.
- **Formatting.** The file is edited as a `JsonNode` tree: key order, unknown keys, and numbers are preserved, and the
  indentation and line endings are detected and reused. `System.Text.Json` cannot keep comments (rejected, as the format is
  strict), inline array layout, string escape spelling, or a byte order mark. A file with nothing to change is not rewritten.
- **Atomic write without another tool's code.** The RepoConfig tool's rollback batch lives in that tool, and tools may link
  only `tools/Shared`, so this tool has its own small writer: temporary sibling, flush, content check, rename, then a
  best-effort flush of the directory. With one file, rollback is leaving the original untouched and removing the temporary file
  and every directory the run created. The file
  must still have the planned content at the rename, or nothing is written.
- **Boundaries.** The target is resolved with `RepositoryPath` (no `..`, no symbolic link in any segment), is a `.json` file of
  at most 1 MiB, and output goes through `ControlCharacters.Escape`. The tool prints the keys and the configured entries, never
  a value from the user's file, except that environment variable names appear in warnings.
- **Config format stays v1.** `settings` is an additive optional section; existing files stay valid.

## Deferred

- Other agents' settings formats, and a profile for other ecosystems.
- Removing stale managed entries, which needs a safe ownership record.
- Managing `enabled`, `allowRead`, and `allowUnixSockets`.

- Wildcard domains are checked with a heuristic, not the public suffix list: a wildcard needs two labels, and `*.co.uk`-style
  generic second-level names are refused.
