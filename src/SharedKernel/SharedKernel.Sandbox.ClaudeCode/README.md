# PandaBear.SharedKernel.Sandbox.ClaudeCode

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Writes the entries that [PandaBear.SharedKernel.Sandbox](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Sandbox/README.md) plans into a
[Claude Code settings file](https://code.claude.com/docs/en/settings), by default the shared project file
`.claude/settings.json`. The `sharedkernel-sandbox settings` commands are a command-line wrapper over this package.

## Usage

```csharp
using PandaBear.Sandbox;
using PandaBear.Sandbox.ClaudeCode;

var desired = DesiredSettings.Build(configuration, flags, homeDirectories, enforce: true);
var file = ClaudeCodeSettingsFile.Locate(repositoryRoot, desired.Target);
var plan = await file.Plan(desired, cancellationToken);
if (plan.Changes.Count > 0)
{
    file.Write(plan);
}
```

- `Locate` resolves the target under the root, `DefaultTarget` when the configuration names none. It refuses a path
  that is not repository-relative, is not a `.json` file, or is or passes through a symbolic link.
- `Plan` reads the file as strict UTF-8 JSON of at most 1 MiB (no comments, trailing commas, or repeated properties) and
  maps the managed settings to these keys of the [settings reference](https://code.claude.com/docs/en/settings-reference):
  `sandbox.filesystem.allowWrite`, `sandbox.filesystem.denyWrite`, `sandbox.filesystem.denyRead`,
  `sandbox.network.allowedDomains`, `sandbox.excludedCommands`, `sandbox.network.allowAllUnixSockets`, and `env`. A
  managed key of the wrong type fails with the file name and JSON path; every other key stays as it is, and the
  rewritten text keeps the indentation, line endings, and final newline of the file.
- `Write` replaces the file atomically after checking that it still has the content the plan was made from, and leaves
  the original untouched on failure.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Sandbox.ClaudeCode`. C# namespace: `PandaBear.Sandbox.ClaudeCode`. Assembly/project identity: `SharedKernel.Sandbox.ClaudeCode`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
