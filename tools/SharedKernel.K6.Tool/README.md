# PandaBear.SharedKernel.K6.Tool

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Runs the [k6](https://grafana.com/docs/k6/latest/) scenarios that a repository configures in
`.config/sharedkernel-k6.json`, with a local k6 or in a digest-pinned container image. The tool checks the k6
arguments, keeps the target on a loopback host, gives local k6 a minimal environment, writes each run's results to a
directory of its own, and can start, wait for, and stop the process that serves the target.

## Prerequisites

A local `k6` on `PATH`, or, with `--container`, the configured Docker or Podman engine with the configured image
already pulled. Executables resolve only from the absolute directories of `PATH`.

## Installation

Install a validated candidate as a local tool from a trusted feed. Add the feed to `NuGet.Config`, map
`PandaBear.SharedKernel.*` to it, and map every other package, for example `*`, to the existing source. With the
SDK pinned in `global.json`, `dotnet tool install` rejects `--add-source` when package source mapping is enabled.

```text
dotnet tool install PandaBear.SharedKernel.K6.Tool --version <validated-candidate-version>
```

A local tool runs as `dotnet tool run sharedkernel-k6 -- run --scenario admin-smoke`; the `--` keeps `dotnet tool run`
from reading the tool options.

## Usage

```text
sharedkernel-k6 run --scenario admin-smoke                        # local k6
sharedkernel-k6 run --scenario admin-smoke --container            # configured engine and image
sharedkernel-k6 run --scenario admin-smoke -- --vus 2 --duration 10s
sharedkernel-k6 run --scenario upload-scan --root ../app --allow-external-target
sharedkernel-k6 --help
```

`run` takes `--scenario <name>` and optionally `--container`, `--image <ref@sha256:digest>` (requires
`--container`), `--allow-external-target`, `--allow-http-debug`, `--allow-remote-output`, `--root <path>` (default:
the current directory), and k6 arguments after `--`.

`run` reads the scenario, checks the k6 arguments, and resolves the target:

- Without a host, the target is the value of the environment variable that `baseUrlVariable` names.
- With a host, the tool starts the host command in the root with the variable that `readyFileVariable` names set to
  a file in the run directory, waits for the host to write its base URL there, then waits for a success status from
  `healthPath`. An argument `{listenUrl}` becomes `http://127.0.0.1:0`, or `http://0.0.0.0:0` with `--container`; a
  reported `0.0.0.0` or `[::]` becomes `127.0.0.1`. The host's output goes to `host.stdout.log` and
  `host.stderr.log` in the run directory, and the tool stops the host process tree after k6 exits.

k6 then runs `k6 run --no-usage-report --summary-export <run>/k6-summary.json`, with `-e` for the base URL variable,
`K6_NO_USAGE_REPORT=true`, and each `environment` variable that is set, followed by the arguments after `--` and the
script. Each run writes to `<resultsDirectory>/<scenario>-<UTC time>-<id>`, which the tool prints to standard error.
k6 output reaches the terminal unchanged.

With `--container`, the engine runs `run --rm --pull=never --name sharedkernel-k6-<id>`, mounts the script directory
read-only at `/k6` and the run directory at `/results`, and starts the configured image or the `--image` one. A
loopback target becomes `host.docker.internal`; Docker maps it with `--add-host host.docker.internal:host-gateway`,
and Podman provides it.

On Linux and macOS the tool makes the run directory that it creates for a container run writable for the
container user (mode `1777`, with the sticky bit), because the k6 image runs as an unprivileged user that is not the
host user. It changes no other directory, and a local run keeps the default mode. k6 exits 0 when it cannot write its
summary, so a container run that exits 0 without `k6-summary.json` in the run directory fails with exit code `2`.

A command line that starts with `[diagram]` prints how it parses instead of running; an invalid command line exits 1.

## Configuration

`.config/sharedkernel-k6.json` under `--root` is strict JSON of at most 1 MiB with camelCase properties; unknown or
repeated properties, comments, and trailing commas are errors that name the JSON path.

```json
{
  "version": 1,
  "scriptDirectory": "tests/performance/k6",
  "resultsDirectory": "tests/performance/results",
  "container": {
    "engine": "docker",
    "image": "grafana/k6:1.0.0@sha256:<digest>"
  },
  "scenarios": [
    {
      "name": "admin-smoke",
      "script": "scenarios/admin-smoke.js",
      "baseUrlVariable": "API_BASE_URL",
      "environment": ["K6_PROFILE", "K6_VUS"]
    },
    {
      "name": "upload-scan",
      "script": "scenarios/upload-scan.js",
      "baseUrlVariable": "UPLOAD_BASE_URL",
      "host": {
        "command": "dotnet",
        "arguments": ["run", "--project", "benchmarks/UploadHost/UploadHost.csproj", "--", "--urls", "{listenUrl}"],
        "readyFileVariable": "UPLOAD_READY_FILE",
        "healthPath": "/health",
        "readyTimeoutSeconds": 60,
        "healthTimeoutSeconds": 30
      }
    }
  ]
}
```

| Property | Meaning |
| --- | --- |
| `version` | `1`. |
| `scriptDirectory` | Repository-relative directory of the k6 scripts; containers mount it read-only. |
| `resultsDirectory` | Repository-relative directory that receives one directory per run. |
| `container.engine` | `docker` or `podman`. Required by `--container`. |
| `container.image` | k6 image pinned by `@sha256:` digest; `--image` replaces it. |
| `scenarios[].name` | Lowercase letters and digits in words separated by `-`; unique. |
| `scenarios[].script` | Script path relative to `scriptDirectory`. |
| `scenarios[].baseUrlVariable` | k6 variable that receives the base URL; without a host, also the environment variable it is read from. |
| `scenarios[].environment` | Optional environment variables passed to k6 as `-e NAME=value` when set. |
| `scenarios[].host.command` | Command name of the host, without a path. |
| `scenarios[].host.arguments` | Host arguments; `{listenUrl}` becomes the bind address. |
| `scenarios[].host.readyFileVariable` | Environment variable that names the file where the host writes its base URL. |
| `scenarios[].host.healthPath` | Path, starting with `/`, that answers a success status when the host is ready. |
| `scenarios[].host.readyTimeoutSeconds` | Seconds, 1 to 3600, for the host to write its base URL. |
| `scenarios[].host.healthTimeoutSeconds` | Seconds, 1 to 3600, for the health path to answer a success status. |

Paths use `/`, have no empty, `.`, or `..` segments, and resolve under the root without following symbolic links.
Variable names are letters, digits, and `_`; `GH_TOKEN`, `GITHUB_TOKEN`, and `K6_NO_USAGE_REPORT` are rejected.

## Security

- Local k6 starts with an empty environment except `PATHEXT`, `SystemRoot`, and `WINDIR`, so only the `-e` values
  reach the script. The container engine and the host inherit the environment without `GH_TOKEN` and `GITHUB_TOKEN`.
- `environment` values appear on the k6 command line; never name a secret there.
- The target must be an absolute `http://` or `https://` URL without user information, query, or fragment, on a
  loopback host (`localhost`, `127.0.0.0/8`, `[::1]`), unless `--allow-external-target` is passed. The host's
  reported URL follows the same rule. The health probe uses no proxy and follows no redirect.
- k6 arguments after `--` cannot pass `--include-system-env-vars`, `--insecure-skip-tls-verify`, or
  `--summary-export`, or set a scenario variable or `K6_NO_USAGE_REPORT` with `-e` or `--env`. `--http-debug` needs
  `--allow-http-debug`; `--out`, `-o`, `--log-output`, `--console-output`, and `--traces-output` need
  `--allow-remote-output`. Short options stand alone, with any value as the next argument, so `-eNAME=value` and
  bundles such as `-qo` are rejected.
- Container images must be pinned by digest, and the engine never pulls (`--pull=never`).
- Ctrl+C or SIGTERM stops k6, force-removes its container, and stops the host before the tool exits.

## Exit codes

| Code | Meaning |
| --- | --- |
| `0` | k6 succeeded. |
| `1` | Invalid command line or cancellation. |
| `2` | Invalid configuration, a rejected argument or target, a missing executable or script, a host that exited, timed out, or reported an invalid URL, or a container run that exited 0 without writing `k6-summary.json`. |
| `130`, `143` | Ctrl+C or SIGTERM during start-up, or cleanup did not finish within 20 seconds. |
| Other | Exit code of k6 or the container engine, once it started. |

## Package identity and availability

Package ID: `PandaBear.SharedKernel.K6.Tool`. C# namespace: `PandaBear.K6.Tool`. Assembly/project identity: `SharedKernel.K6.Tool`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
