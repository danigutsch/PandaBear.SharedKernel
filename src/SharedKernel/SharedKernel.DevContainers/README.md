# PandaBear.SharedKernel.DevContainers

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Development container host checks. `DevcontainerVerification` starts a repository's dev container with a pinned,
integrity-checked [Dev Container CLI](https://github.com/devcontainers/cli), runs commands inside it, and removes it.
`PodmanDnsPreflight` inspects rootless Podman aardvark-dns state without changing it. The `dotnet repo` tool
exposes them as `verify-devcontainer` and `verify-podman-dns`; its
[README](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/tools/SharedKernel.RepoConfig.Tool/README.md)
describes the checks and their output.

## Usage

```csharp
using PandaBear.DevContainers;

var exitCode = await PodmanDnsPreflight.Run(PodmanDnsHost.Current(), quiet: false, Console.Out, Console.Error, cancellationToken);
```

- `DevcontainerHost` and `PodmanDnsHost` describe the machine: the executables, the platform, the process runner, and
  the HTTP client that downloads the pinned CLI. `Current()` describes this machine; tests pass fakes.
- `DevcontainerCliPins` pins the Node.js runtime by SHA-256 and the `@devcontainers/cli` package by its npm integrity.
- Both checks write their report to the writers they are given and return the exit code that the README of the tool
  documents.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.DevContainers`. C# namespace: `PandaBear.DevContainers`. Assembly/project identity: `SharedKernel.DevContainers`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
