# PandaBear.SharedKernel.Packages.Tool

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Compares the packages that a repository declares in `.config/sharedkernel-packages.json`, per package manager, with the
packages that are installed. It reads and reports; it installs, removes, and updates nothing. It refuses a declaration in
which one package has more than one owner, because two package managers would then fight over it.

## Prerequisites

The tools of the declared managers on `PATH`: `dpkg-query` for `apt`, `cargo`, `npm`, `flatpak`, `snap`, and `brew`. Executables resolve
only from the absolute directories of `PATH`. A manager with no declared package needs no tool.

## Installation

Install a validated candidate as a local tool from a trusted feed. Add the feed to `NuGet.Config`, map
`PandaBear.SharedKernel.*` to it, and map every other package, for example `*`, to the existing source.

```text
dotnet tool install PandaBear.SharedKernel.Packages.Tool --version <validated-candidate-version>
```

A local tool runs as `dotnet tool run sharedkernel-packages -- plan`.

## Usage

```text
sharedkernel-packages plan
sharedkernel-packages plan --root ../machine-config
sharedkernel-packages --help
```

`updates` judges each declared, installed, unpinned cargo and npm package against its registry's newest stable version,
advisories from [osv.dev](https://osv.dev), and a minimum release age. It is the only command that uses the network, and
only with `--allow-network`:

```text
sharedkernel-packages updates --allow-network
sharedkernel-packages updates --allow-network --minimum-release-age-days 14
sharedkernel-packages updates --allow-network --root ../machine-config
```

It sends the package names and versions to `api.osv.dev`, `crates.io` and `registry.npmjs.org` over HTTPS. Without
`--allow-network` it refuses, says so, and contacts nothing. Each line is `manager: Decision name installed -> newest:
reason`, where the decision is `Allow`, `Hold` (too new), `Urgent` (the installed version has an advisory that the newest
does not) or `Block` (the newest has an advisory). If any lookup fails, no decision is printed for that manager and the
exit code is 2. A package that its registry does not know (a git or path crate, a linked npm package) is reported as "not on the registry; not checked", the other packages are still judged, and the exit code is 2. apt, Flatpak and other managers have no release-date source and are reported as unsupported and skipped, also with exit code 2, so exit 0 always means a complete result.
The age comes from `--minimum-release-age-days` or, without it, `minimumReleaseAgeDays` in the configuration; one of the
two is required.

```text
cargo: Allow bat 0.24.0 -> 0.26.1: The candidate has been public for 30 days; the minimum is 7 days.
cargo: Hold fd-find 9.0.0 -> 10.0.0: The candidate is too new; it can be installed in 5 days.
```

`plan` takes `--root <path>`, the repository root, which defaults to the current directory. It prints one line for each
declared package that is not met, then a summary. When a manager cannot be read, the other managers are still reported, the
error is printed, and there is no summary:

```text
apt: install libreoffice
cargo: change ripgrep from 13.0.0 to 14.1.0
2 of 14 declared packages need a change.
```

## Configuration

`.config/sharedkernel-packages.json` is strict JSON with `"version": 1`:

```json
{
  "version": 1,
  "packages": {
    "apt": ["git", "libreoffice"],
    "cargo": ["ripgrep=14.1.0", "bat"],
    "npm": ["corepack"],
    "flatpak": ["org.telegram.desktop"],
    "snap": ["firefox"],
    "brew": ["ripgrep"]
  },
  "minimumReleaseAgeDays": 7
}
```

- The managers are `apt`, `cargo`, `npm`, `flatpak`, `snap`, and `brew`. Each entry is a package name, or `name=version` to require
  exactly that version.
- `minimumReleaseAgeDays` is optional, a whole number from 0 to 3650, and read by `updates` only.
- **One owner per package.** A name that appears under two managers, or twice under one, is an error that names the
  package and the managers, and nothing is run. The owner is the name: a pin does not make two declarations different.
- Versions are compared as text, exactly. apt names include an architecture, such as `libc6:amd64`, when a bare name is
  ambiguous. A Homebrew formula with more than one installed version is an error until `brew cleanup` removes the old
  ones; brew prints nothing when it has nothing installed, which reads as an empty list.

## Exit codes

| Code | Meaning |
| --- | --- |
| 0 | Every declared package is installed. |
| 1 | A declared package is missing or at another version. Nothing else is wrong. |
| 2 | The result cannot be known: a package has more than one owner, the configuration is invalid or missing, a tool is not on `PATH`, or a tool failed, ran too long, wrote too much, or printed something unreadable. |

For `updates`, 0 means every declared, installed, unpinned package was checked and the decisions were printed, and 2 means the result
is unknown or incomplete: the network was not allowed, the age is missing or invalid, a tool is missing or unreadable, any
lookup failed, or a package or manager could not be checked. A `Block`
or `Hold` decision is information, not a failure.

## Limits

- `updates` cannot order versions, so a newest stable version that differs from the installed one is listed even when it is
  older.
- OSV answers for the exact versions that are asked about; an advisory that OSV does not know of is not found.

- A tool that leaves a background process holding its output open after it exits is an error; that background process is
  not stopped.
- `plan` reads the installed state and nothing else. A package that is installed by a second manager and not declared
  for it is not reported.
