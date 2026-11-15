# SharedKernel.PlaywrightMcp

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

The engine of the [Playwright MCP tool](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/tools/SharedKernel.PlaywrightMcp.Tool/README.md): runs the
digest-pinned [Playwright MCP](https://github.com/microsoft/playwright-mcp) container image as a stdio MCP server
through a local Docker engine or rootless Podman, and pulls or removes that image.

The `sharedkernel-playwright-mcp` tool is a thin command-line wrapper over this package. The tool README documents the
environment variables, container limits, exit codes, and security checks; they belong to this engine and apply to any
host that calls it.

## Usage

`PlaywrightMcpApplication` takes the environment lookup, the executable lookup, and the process runner it uses, so a
host supplies them and tests replace them:

```csharp
using PandaBear.PlaywrightMcp;

var application = new PlaywrightMcpApplication(
    Environment.GetEnvironmentVariable,
    PlaywrightMcpApplication.ResolveExecutable,
    PlaywrightMcpApplication.RunProcess);

var exitCode = await application.Prepare(Console.Error, cancellationToken);
```

- `Serve`, `Prepare`, and `Clean` return an exit code and write diagnostics to the given writer. They return 2 for an
  invalid environment and 1 for an engine, I/O, cleanup, or cancellation failure; otherwise the engine's or server's
  exit code.
- `ResolveExecutable` finds `docker` or `podman` only in absolute `PATH` directories.
- `RunProcess` runs a process, forwards or collects its output, and kills its process tree on cancellation.
- `Image` is the pinned image reference.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.PlaywrightMcp`. C# namespace: `PandaBear.PlaywrightMcp`. Assembly/project identity: `SharedKernel.PlaywrightMcp`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
