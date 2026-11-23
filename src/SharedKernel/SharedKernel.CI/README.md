# PandaBear.SharedKernel.CI

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

The forge-neutral CI engine behind `dotnet ci`: it selects the test projects a change affects, runs them in
slices with their services and evidence, checks changed projects without sockets, gates changed-code coverage,
classifies and summarizes warnings, stages pinned package candidates, prunes old prereleases, runs Stryker.NET, builds
OpenAPI documents, and verifies Conventional Commit messages. The consuming repository supplies its policy in
`.config/sharedkernel-ci.json`. The tool's
[README](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/tools/SharedKernel.CI.Tool/README.md)
describes the configuration and every command; this package applies them to any host.

## Usage

```csharp
using PandaBear.CI;
using PandaBear.CI.GitHub;
using PandaBear.Processes;

var machine = new CiMachine(new SystemProcessRunner(), PathExecutableResolver.Default, TimeProvider.System);
var plan = await new TestSelector(machine).Select(
    root, Path.Combine(root, "TestResults/selection"), baseRevision,
    new GitHubActionsOutputs(Environment.GetEnvironmentVariable("GITHUB_OUTPUT")!),
    Console.Out, Console.Error, cancellationToken);
```

- `CiMachine` holds the side effects: the `IProcessRunner` and `IExecutableResolver` of
  `PandaBear.SharedKernel.Processes` that run Git, dotnet, PowerShell, and Stryker, the start of a slice's service
  process, and the `TimeProvider`. Tests pass fakes and start nothing.
- The operations are `TestSelector`, `SliceRunner`, `LocalCheckRunner`, `OpenApiGenerator`, `PlaywrightInstaller`,
  `CoverageVerifier`, `MutationTestRunner`, `CandidateStager`, `PrereleasePruner`, `CommitMessageVerifier`, and
  `WarningReports`. Each takes the repository root and a settings record where it needs one, writes results and
  progress to the `TextWriter`s it is given, and throws `InvalidOperationException`, `InvalidDataException`,
  `ArgumentException`, `HttpRequestException`, or `TimeoutException` for a failure, with a message meant for the user.
- `CiConfigurationFile.Read`, `SolutionGraph.Load`, and `ProjectSelector` read the configuration and the solution and
  select projects without Git, for repository convention tests. `SelectionPlanFile` reads and writes `selection.json`.
- Forge-specific parts are seams that provider packages implement: `IRunOutputs` (step outputs), `IStepSummary` (step
  summary Markdown), `IReleaseStore` (list and delete releases), and `IReleaseAssets` (public download addresses of
  release assets). `PandaBear.SharedKernel.CI.GitHub` implements them for GitHub and GitHub Actions. The engine emits
  no workflow annotations, so it defines no annotation seam.
- The engine reads no environment variable and writes to no console. Service processes never receive a forge token (`GH_TOKEN`,
  `GITHUB_TOKEN`, `FORGEJO_TOKEN`, `GH_ENTERPRISE_TOKEN`, or `GITHUB_ENTERPRISE_TOKEN`); `PandaBear.SharedKernel.Processes`
  removes them, and a service variable cannot be named after one.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.CI`. C# namespace: `PandaBear.CI`. Assembly/project identity: `SharedKernel.CI`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
