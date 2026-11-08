# PandaBear.SharedKernel.Packages.Tool

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Compares the packages that a repository declares in `.config/sharedkernel-packages.json`, per package manager, with the
packages that are installed. It reads and reports; it installs, removes, and updates nothing. It refuses a declaration in
which one package has more than one owner, because two package managers would then fight over it.

## Prerequisites

The tools of the declared managers on `PATH`: `dpkg-query` for `apt`, `cargo`, `npm`, and `flatpak`. Executables resolve
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
    "flatpak": ["org.telegram.desktop"]
  }
}
```

- The managers are `apt`, `cargo`, `npm`, and `flatpak`. Each entry is a package name, or `name=version` to require
  exactly that version.
- **One owner per package.** A name that appears under two managers, or twice under one, is an error that names the
  package and the managers, and nothing is run. The owner is the name: a pin does not make two declarations different.
- Versions are compared as text, exactly. apt names include an architecture, such as `libc6:amd64`, when a bare name is
  ambiguous.

## Exit codes

| Code | Meaning |
| --- | --- |
| 0 | Every declared package is installed. |
| 1 | A declared package is missing or at another version. Nothing else is wrong. |
| 2 | The result cannot be known: a package has more than one owner, the configuration is invalid or missing, a tool is not on `PATH`, or a tool failed, ran too long, wrote too much, or printed something unreadable. |

## Limits

- A tool that leaves a background process holding its output open after it exits is an error; that background process is
  not stopped.
- The tool reads the installed state and nothing else. A package that is installed by a second manager and not declared
  for it is not reported.
