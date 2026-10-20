# PandaBear.SharedKernel.PlaywrightMcp.Tool

Reusable Playwright MCP browser automation tool.

## Usage

Install from a trusted private candidate feed using `dotnet tool install PandaBear.SharedKernel.PlaywrightMcp.Tool --tool-path .tools --version <validated-candidate-version> --add-source <private-candidate-feed>`. Run `sharedkernel-playwright-mcp prepare` explicitly to pull the pinned container image, then configure the MCP client to launch `sharedkernel-playwright-mcp`. Docker or Podman must be available locally. Set `PLAYWRIGHT_MCP_ENGINE=docker` or `podman` when both exist. Network access defaults off; `PLAYWRIGHT_MCP_NETWORK_ACCESS=1` enables it deliberately. Image responses default to `omit`; `PLAYWRIGHT_MCP_IMAGE_RESPONSES=allow` enables them. Remote engine overrides are rejected. `clean` removes the prepared image; inspect other local uses before running it.

## Run

After installing the explicitly selected private candidate and configuring the local engine:

```text
sharedkernel-playwright-mcp prepare
sharedkernel-playwright-mcp --version
```

`prepare` downloads the pinned container image; normal execution uses `--pull=never`. Start the command without arguments from the MCP client for its stdio session.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.PlaywrightMcp.Tool`. C# namespace: `PandaBear.PlaywrightMcp.Tool`. Assembly/project identity: `SharedKernel.PlaywrightMcp.Tool`.

Consume a validated private candidate at an explicitly selected version. Registry publication is deferred; no public NuGet availability is implied.
