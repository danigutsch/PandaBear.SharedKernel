# PandaBear.SharedKernel.PlaywrightMcp.Tool

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Runs the pinned [Playwright MCP](https://github.com/microsoft/playwright-mcp) container image as a local stdio MCP server
through Docker or rootless Podman.

## Prerequisites

Docker whose current context uses a local endpoint, or rootless Podman, running Linux containers.

## Installation

Install a validated candidate as a local tool from a trusted feed. Add the feed to `NuGet.Config`, map
`PandaBear.SharedKernel.*` to it, and map every other package, for example `*`, to the existing source. With the
SDK pinned in `global.json`, `dotnet tool install` rejects `--add-source` when package source mapping is enabled.

```text
dotnet tool install PandaBear.SharedKernel.PlaywrightMcp.Tool --version <validated-candidate-version>
```

A local tool runs as `dotnet tool run sharedkernel-playwright-mcp -- <command>`.

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
standard error. `prepare` and `clean` write nothing to standard output; each line of the container engine's output
and errors goes to standard error as the engine writes it. `clean` removes the image from the local engine cache;
check other local uses before running it.

Configuration comes from the environment:

| Variable | Values |
| --- | --- |
| `PLAYWRIGHT_MCP_ENGINE` | `docker` or `podman`; required when both are installed. |
| `PLAYWRIGHT_MCP_NETWORK_ACCESS` | `1` enables container networking; default `0`. |
| `PLAYWRIGHT_MCP_IMAGE_RESPONSES` | `allow` sends images to the MCP client; default `omit`. |

## Limits

`serve` runs the container with fixed limits:

- a read-only root file system;
- 2 GiB of memory and 1 GiB of shared memory;
- at most 256 processes and threads (PIDs);
- a 512 MiB `/tmp` tmpfs and a 128 MiB `/home/node` tmpfs;
- a 10 MiB cap on Playwright output.

## Exit codes

| Code | Meaning |
| --- | --- |
| `0` | Success. |
| `1` | Invalid command line, failed engine check, runtime or container cleanup failure, or cancellation. |
| `2` | Invalid configuration: unsupported environment value, missing or ambiguous engine, or a remote engine override. |
| `130`, `143` | Ctrl+C or SIGTERM during start-up, or container cleanup did not finish within 20 seconds. |
| Other | Exit code of the container engine or the MCP server. |

A container engine or MCP server that exits or fails after the tool has observed Ctrl+C or SIGTERM counts as the
cancellation.

## Security

- Remote engine overrides are rejected: `DOCKER_HOST` and `DOCKER_CONTEXT` for Docker, `CONTAINER_HOST` and
  `CONTAINER_CONNECTION` for Podman. The current Docker context must use a local socket or pipe, and Podman must run
  rootless.
- The container drops all Linux capabilities and runs with `no-new-privileges`.
- Command-line arguments never reach the container. Networking and image responses stay off unless enabled.
- Ctrl+C or SIGTERM stops the server and force-removes its container before the tool exits.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.PlaywrightMcp.Tool`. C# namespace: `PandaBear.PlaywrightMcp.Tool`. Assembly/project identity: `SharedKernel.PlaywrightMcp.Tool`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
