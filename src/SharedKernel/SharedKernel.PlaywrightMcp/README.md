# PandaBear.SharedKernel.PlaywrightMcp

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Runs the digest-pinned [Playwright MCP](https://github.com/microsoft/playwright-mcp) container image as a stdio MCP
server on a local Docker engine or rootless Podman, and pulls or removes that image. The `dotnet browsermcp`
tool is a command-line wrapper over this package; its
[README](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/tools/SharedKernel.PlaywrightMcp.Tool/README.md)
lists the container limits and security checks, which this package applies to any host.

## Usage

```csharp
using SharedKernel.PlaywrightMcp;
using SharedKernel.Processes;

var server = new PlaywrightMcpServer(new SystemProcessRunner(), PathExecutableResolver.Default);
var settings = PlaywrightMcpSettings.FromEnvironment(Environment.GetEnvironmentVariable);

var exitCode = await server.Prepare(settings, Console.Error, cancellationToken);
```

- `PlaywrightMcpServer` takes the process runner and executable resolver from `PandaBear.SharedKernel.Processes`, so
  tests replace them and start no container engine.
- `Serve`, `Prepare`, and `Clean` take `PlaywrightMcpSettings` and a diagnostics writer and return an exit code: the
  engine's or server's own exit code, 2 for invalid settings (an unsupported value, a missing or ambiguous engine, or a
  remote engine override), and 1 for an engine check, I/O, cleanup, or cancellation failure.
- `PlaywrightMcpSettings` holds the values of the `PLAYWRIGHT_MCP_ENGINE`, `PLAYWRIGHT_MCP_NETWORK_ACCESS`, and
  `PLAYWRIGHT_MCP_IMAGE_RESPONSES` variables and of the remote engine overrides that the server refuses (`DOCKER_HOST`,
  `DOCKER_CONTEXT`, `CONTAINER_HOST`, `CONTAINER_CONNECTION`). `FromEnvironment` reads them.
- `Image` is the pinned image reference.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.PlaywrightMcp`. C# namespace: `SharedKernel.PlaywrightMcp`. Assembly/project identity: `SharedKernel.PlaywrightMcp`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
