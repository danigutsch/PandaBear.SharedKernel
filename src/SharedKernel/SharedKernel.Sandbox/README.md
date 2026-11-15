# PandaBear.SharedKernel.Sandbox

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Probes what a sandboxed environment allows for .NET tools, reads the consumer policy in
`.config/sharedkernel-sandbox.json`, and plans the sandbox entries an agent settings file needs, independent of the
agent's file format. The `sharedkernel-sandbox` tool is a command-line wrapper over this package and
[PandaBear.SharedKernel.Sandbox.ClaudeCode](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Sandbox.ClaudeCode/README.md); its
[README](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/tools/SharedKernel.Sandbox.Tool/README.md)
describes the configuration format, the probes, and the risk flags.

## Usage

```csharp
using PandaBear.Sandbox;

var configuration = await SandboxConfigurationFile.Read(repositoryRoot, cancellationToken);
var exitCode = await new SandboxDoctor(new SystemSandboxEnvironment()).Run(configuration, Console.Out, cancellationToken);
```

- `SandboxDoctor` tries each operation through `ISandboxEnvironment` and undoes it: a Unix domain socket and a named
  pipe, writing the temporary and NuGet package folders, and reading each configured denied path. `Probe` returns the
  `ProbeResult` list; `Run` writes one line per probe and a summary, and returns 0, or 1 when a required probe has a bad
  outcome. `SystemSandboxEnvironment` tries the operations for real and reads `NUGET_PACKAGES`, the home folder, and the
  account database; tests pass a fake.
- `SandboxConfigurationFile.Read` reads and validates `.config/sharedkernel-sandbox.json` (version 1) under a root
  without following symbolic links, and returns `null` when it does not exist. Errors name the file and the JSON path.
- `DesiredSettings.Build` turns the `settings` section, the built-in `dotnet` profile, and the accepted
  `SettingsRiskFlags` into the entries the settings file should contain. A risky entry (excluded commands, all Unix
  sockets, removed denied reads, a broad or sensitive write path, such as the home folder or `~/.ssh`) is refused
  unless its flag is set, or only warned about when the refusal is not enforced.
- `SettingsMerger.Plan` compares the desired entries with an `ISandboxSettingsDocument`, which a format library
  implements, and returns a `SettingsPlan`: the `SettingsChange` list, the warnings, and the new content. It only adds
  entries, and removes only the denied reads it is asked to; every other key stays as it is.

The built-in `dotnet` profile lives here rather than in a separate library, because its options are part of the
version 1 configuration format and the doctor's NuGet folder probe and socket advice are written for .NET tools.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Sandbox`. C# namespace: `PandaBear.Sandbox`. Assembly/project identity: `SharedKernel.Sandbox`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
