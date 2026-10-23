# PandaBear.SharedKernel.PlaywrightMcp.Tool

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Runs the pinned [Playwright MCP](https://github.com/microsoft/playwright-mcp) container image as a local stdio MCP server
through Docker or rootless Podman.

## Prerequisites

Docker whose current context uses a local endpoint, or rootless Podman, running Linux containers.

## Installation

Install a validated candidate from a trusted private feed:

```text
dotnet tool install PandaBear.SharedKernel.PlaywrightMcp.Tool --tool-path .tools --version <validated-candidate-version> --add-source <private-candidate-feed>
```

## Usage

```text
sharedkernel-playwright-mcp prepare    # pull the pinned image
sharedkernel-playwright-mcp serve      # run the stdio MCP server
sharedkernel-playwright-mcp clean      # remove the pinned image
sharedkernel-playwright-mcp --help
sharedkernel-playwright-mcp --version
```

Run `prepare` first; `serve` starts the image with `--pull=never`. Configure the MCP client to start
`sharedkernel-playwright-mcp serve`. `serve` writes only the MCP protocol to standard output and diagnostics to
standard error. `clean` removes the image from the local engine cache; check other local uses before running it.

Configuration comes from the environment:

| Variable | Values |
| --- | --- |
| `PLAYWRIGHT_MCP_ENGINE` | `docker` or `podman`; required when both are installed. |
| `PLAYWRIGHT_MCP_NETWORK_ACCESS` | `1` enables container networking; default `0`. |
| `PLAYWRIGHT_MCP_IMAGE_RESPONSES` | `allow` sends images to the MCP client; default `omit`. |

## Exit codes

| Code | Meaning |
| --- | --- |
| `0` | Success. |
| `1` | Invalid command line, failed engine check, runtime or container cleanup failure, or cancellation. |
| `2` | Invalid configuration: unsupported environment value, missing or ambiguous engine, or a remote engine override. |
| `130`, `143` | Ctrl+C or SIGTERM during start-up, or container cleanup did not finish within 20 seconds. |
| Other | Exit code of the container engine or the MCP server. |

## Security

- Remote engine overrides are rejected: `DOCKER_HOST` and `DOCKER_CONTEXT` for Docker, `CONTAINER_HOST` and
  `CONTAINER_CONNECTION` for Podman. The current Docker context must use a local socket or pipe, and Podman must run
  rootless.
- Command-line arguments never reach the container. Networking and image responses stay off unless enabled.
- Ctrl+C or SIGTERM stops the server and force-removes its container before the tool exits.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.PlaywrightMcp.Tool`. C# namespace: `PandaBear.PlaywrightMcp.Tool`. Assembly/project identity: `SharedKernel.PlaywrightMcp.Tool`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
