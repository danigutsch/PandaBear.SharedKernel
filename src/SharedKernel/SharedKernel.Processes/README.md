# PandaBear.SharedKernel.Processes

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Child process execution and executable resolution for hosts that start other programs, such as command-line tools.
Targets .NET 10, has no package dependencies, and is trimming- and NativeAOT-compatible.

```csharp
using System.Diagnostics;
using PandaBear.Processes;

IExecutableResolver resolver = PathExecutableResolver.Default;
IProcessRunner runner = new BoundedProcessRunner();

var git = resolver.Find("git") ?? throw new InvalidOperationException("git is not installed.");
var startInfo = new ProcessStartInfo(git);
startInfo.ArgumentList.Add("--version");
var output = await runner.Run(startInfo, forward: null, cancellationToken);
```

- `IProcessRunner` runs the process that a `ProcessStartInfo` describes and returns a `ProcessOutput`: the exit code and
  the collected standard output and standard error. With a `forward` writer it writes each output line there as it
  arrives and collects nothing. The file name must be a fully qualified path. Hosts and libraries take the interface, so
  tests replace it with a fake and never start a process.
- `SystemProcessRunner` runs the process as described, without a time or output limit, so it suits a long-running server
  whose standard streams stay attached to the host. Cancellation kills the whole process tree and still throws the
  cancellation.
- `BoundedProcessRunner` is for quick queries, such as a package manager listing: it redirects and captures both output
  streams, closes a redirected standard input, ends the process after two minutes or above 8 Mi characters on either
  stream, and reports a process that leaves its output open after it exits.
- `IExecutableResolver` finds the executable that a command name starts. `PathExecutableResolver` looks only in the
  absolute directories of `PATH`, never in the application directory, the working directory, or a relative `PATH`
  entry, so a checkout cannot supply the executable. On Unix a file needs an execute bit; on Windows `<command>.exe` is
  tried first. `PathExecutableResolver.Default` reads the process `PATH` on every call, and `Resolve` returns a fully
  qualified file name unchanged or throws when no directory contains the command.

- `ProcessTree` serves hosts that keep a child running, such as a service started for the length of a test run:
  `Start` reports a start failure as `InvalidOperationException` (`Failed to start <file>: <reason>`), and `Terminate(process,
  exitTimeout)` kills the whole tree unless it has exited and waits a bounded time, ignoring cleanup failures so the
  failure or cancellation that started the cleanup is what the caller sees.
- `ProcessEnvironment` keeps inherited values away from a child: `RemoveForgeTokens` drops every forge token variable
  (`GH_TOKEN`, `GITHUB_TOKEN`, and `FORGEJO_TOKEN`, listed in `ForgeTokenVariableNames`), so an engine keeps tokens away
  from its children without depending on a forge library, and `IsForgeTokenVariable` tells a configuration that names
  one apart. `RemoveGitVariables` drops every `GIT_*` variable, which could point Git at another repository, index, or
  configuration, and `Remove(startInfo, names)` drops named variables. Names compare without regard to case.

The runners pass arguments one by one without a shell. They set no environment; a caller that parses a tool's output
sets the locale on the start information, as `PandaBear.SharedKernel.Packages` does.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Processes`. C# namespace: `PandaBear.Processes`. Assembly/project identity: `SharedKernel.Processes`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
