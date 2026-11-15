# PandaBear.SharedKernel.Repositories

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Forge-neutral repository governance. It reads the consumer configuration in `.config/sharedkernel-repo.json`,
plans and applies the managed baseline files and Git settings, starts new repositories, keeps the roadmap model under
`roadmap/`, and verifies repository content. The `sharedkernel-repo` tool is a command-line wrapper over this
package and its providers; its
[README](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/tools/SharedKernel.RepoConfig.Tool/README.md)
documents the configuration format and every check.

## Usage

```csharp
using PandaBear.Repositories;

var links = await LinkVerifier.Verify(rootPath, [], cancellationToken);
foreach (var issue in links.Issues)
{
    Console.Error.WriteLine($"{issue.Path}: {issue.Message}");
}
```

- **Configuration:** `ConsumerConfigurationFile` names the file and parses its `lockExemptProjects`; the verifiers and
  `RepositoryBaseline` read their own sections and throw `InvalidOperationException` for a missing or invalid one.
- **Verifiers** return `VerificationIssue` lists in path order: `LinkVerifier` (with the scanned file count),
  `GherkinVerifier`, `JsonVerifier`, `TextEncodingVerifier`, `LineEndingVerifier`, `ActionPinVerifier`,
  `DependabotIgnoreVerifier`, and `LicenseExceptionVerifier` (with the approved package URLs).
- **Baseline and new repositories:** `RepositoryBaseline.Verify` and `Apply` plan the managed files and Git settings
  and write them in one rollback batch; `RepositoryScaffold.Run` writes a new repository's baseline, configuration, and
  Git setup from `RepositoryFeature` and `RepositoryLanguage`. Both report to the writers they are given and return 0
  or 1.
- **Security:** `RepositorySecurity.Run` verifies `.config/security-baseline.json` and, with a report directory, runs
  `SecurityScanner` (Gitleaks); it returns 0 clean, 1 findings, 2 invalid input, or 3 scanner failure.
- **Roadmap:** `RoadmapProject` loads and queries the items, `RoadmapVerifier` checks the structure and graph,
  `RoadmapInitializer` creates the missing files from `RoadmapTemplates`, and `RoadmapConfigSetter` sets a value.
  `RollbackFileWriteBatch` and `AtomicFileWrite` write several files atomically under a lock, and
  `RoadmapGitRepository` reads Git state through bounded, time-limited processes that never receive a GitHub token.
- **Forge seams:** `IRepositoryLabelStore` is a repository's labels on a forge, and `RepositoryLabelSync` creates and
  updates the configured labels through it and never deletes one. A provider package, such as
  `PandaBear.SharedKernel.Repositories.GitHub`, implements the seam for its forge.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Repositories`. C# namespace: `PandaBear.Repositories`. Assembly/project identity: `SharedKernel.Repositories`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
