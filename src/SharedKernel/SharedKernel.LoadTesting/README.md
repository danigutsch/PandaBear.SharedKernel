# PandaBear.SharedKernel.LoadTesting

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Runs the load-test scenarios that a repository configures in a strict JSON file through a pluggable load generator.
The runner reads and validates the configuration, keeps the target on a loopback host, creates a directory of its own
for each run's results, and can start, wait for, and stop the process that serves the target. The load generator, such
as k6 from `PandaBear.SharedKernel.LoadTesting.K6`, checks its own arguments and runs. The `dotnet loadtest` tool is a
command-line wrapper over this package and the k6 provider; its
[README](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/tools/SharedKernel.K6.Tool/README.md)
describes the configuration format, the run layout, and the security checks, which this package applies to any host.

## Usage

```csharp
using PandaBear.LoadTesting;
using PandaBear.LoadTesting.K6;
using PandaBear.Processes;

using var healthCheckHandler = new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false };
var runner = new LoadTestRunner(new K6LoadGenerator(new SystemProcessRunner()), PathExecutableResolver.Default, healthCheckHandler);

var exitCode = await runner.Run(
    new LoadTestSettings
    {
        Root = repositoryRoot,
        ConfigurationPath = ".config/sharedkernel-k6.json",
        Scenario = "admin-smoke",
        Environment = environment,
    },
    Console.Error,
    cancellationToken);
```

- `LoadTestRunner` takes the `ILoadGenerator`, the executable resolver that finds the generator, the container engine,
  and the host command, and the `HttpMessageHandler` that sends the host's health probes. The caller owns the handler;
  it should use no proxy and follow no redirect, so the probe reaches only the host.
- `Run` returns the generator's exit code and writes the run directory and the host address to the diagnostics writer.
  It throws `InvalidOperationException` for an invalid configuration, argument, image, or target, a missing executable
  or script, a host that exited, timed out, or reported an invalid address, or a process that could not start.
- `LoadTestSettings` holds the root, the configuration file path, the scenario, the container choice and image
  override, what the run allows, the generator arguments, and the environment. The runner reads variables only from
  `Environment`, never from the process.
- `ILoadGenerator` names the local command, the variables it always sets, checks its arguments, and runs a prepared
  `LoadGeneratorRun`. `LoadTestContainer` is the engine (`docker` or `podman`) and digest-pinned image of a container
  run, and the host alias through which a container reaches the machine.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.LoadTesting`. C# namespace: `PandaBear.LoadTesting`. Assembly/project identity: `SharedKernel.LoadTesting`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
