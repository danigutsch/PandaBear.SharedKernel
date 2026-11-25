# PandaBear.SharedKernel.LoadTesting.K6

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Runs [k6](https://grafana.com/docs/k6/latest/) as the load generator of `PandaBear.SharedKernel.LoadTesting`. The
`dotnet loadtest` tool is a command-line wrapper over both packages; its
[README](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/tools/SharedKernel.K6.Tool/README.md)
describes the run and its security checks.

## Usage

```csharp
using SharedKernel.LoadTesting;
using SharedKernel.LoadTesting.K6;
using SharedKernel.Processes;

var runner = new LoadTestRunner(new K6LoadGenerator(new SystemProcessRunner()), PathExecutableResolver.Default, healthCheckHandler);
```

- `K6LoadGenerator` takes the process runner from `PandaBear.SharedKernel.Processes`, so tests replace it and start no
  k6 or container engine.
- k6 runs as `k6 run --no-usage-report --summary-export <run>/k6-summary.json`, with `-e NAME=value` for the base URL,
  `K6_NO_USAGE_REPORT=true`, and each set scenario variable, then the checked arguments and the script. Local k6 starts
  with an empty environment except `PATHEXT`, `SystemRoot`, and `WINDIR`.
- A container run uses `run --rm --pull=never --name sharedkernel-k6-<id>`, adds
  `--add-host host.docker.internal:host-gateway` for Docker, mounts the script directory read-only at `/k6` and the run
  directory at `/results`, and removes every forge token variable (`ProcessEnvironment.ForgeTokenVariableNames`) from the
  engine's environment.
  Cancellation force-removes the container. k6 exits 0 when it cannot write the summary, so a container run that exits 0 without
  `k6-summary.json` throws `InvalidOperationException`.
- The arguments cannot pass `--include-system-env-vars`, `--insecure-skip-tls-verify`, or `--summary-export`, or set a
  controlled variable with `-e` or `--env`. `--http-debug` needs `AllowHttpDebug`; `--out`, `-o`, `--log-output`,
  `--console-output`, and `--traces-output` need `AllowRemoteOutput`. Short options stand alone, with any value as the
  next argument.

The flags were checked against the [k6 options reference](https://grafana.com/docs/k6/latest/using-k6/k6-options/reference/).

## Package identity and availability

Package ID: `PandaBear.SharedKernel.LoadTesting.K6`. C# namespace: `SharedKernel.LoadTesting.K6`. Assembly/project identity: `SharedKernel.LoadTesting.K6`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
