# PandaBear.SharedKernel.Repositories.GitHub

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

GitHub provider for `PandaBear.SharedKernel.Repositories`. It synchronizes repository labels, projects roadmap items
to GitHub issues and Projects, reconciles and imports issues, runs the integrity-bound roadmap automation steps, links
sub-issues, and writes and submits dependency snapshots. Every request goes through a `GitHubConnection`, which uses
the GitHub REST headers of `PandaBear.SharedKernel.GitHub`.

## Usage

```csharp
using PandaBear.GitHub;
using PandaBear.Repositories;
using PandaBear.Repositories.GitHub;

using var connection = new GitHubConnection(
    new HttpClientHandler { AllowAutoRedirect = false },
    new ProductInfoHeaderValue("my-tool", "1.0"),
    GitHubToken.Find(Environment.GetEnvironmentVariable),
    "Set GH_TOKEN or GITHUB_TOKEN before syncing labels.");
var store = new GitHubLabelStore("owner/repository", connection);
await RepositoryLabelSync.Run(rootPath, store, force: false, Console.Out, cancellationToken);
```

- `GitHubConnection` borrows an `HttpClient` the caller configured, or creates one from an injected
  `HttpMessageHandler` and token the first time an operation sends a request, so validation errors come before a
  missing-token error. The caller disposes it.
- `GitHubLabelStore` implements `IRepositoryLabelStore` over the
  [REST labels API](https://docs.github.com/rest/issues/labels).
- `GitHubRoadmapSyncer`, `GitHubRoadmapReconciler`, and `GitHubRoadmapIntake` preview or apply roadmap projection,
  reconciliation, and import. `RoadmapAutomationService` runs the plan, validate, apply, seal, verify, and publish
  steps of the roadmap maintenance workflow, and `RoadmapAutomationPlan` and `RoadmapAutomationResultSeal` are the
  reviewed artifacts it reads and writes.
- `GitHubSubissueLinker` links an issue to a parent through the
  [sub-issues API](https://docs.github.com/rest/issues/sub-issues).
- `GitHubDependencySnapshot.TryWrite` writes a
  [dependency submission](https://docs.github.com/rest/dependency-graph/dependency-submission) snapshot from the
  committed NuGet lock files of a reviewed commit, and `GitHubDependencySubmission.Run` submits it.
- `GitHubRoadmapReconcileIssue`, `GitHubRoadmapReconcileRelation`, and `GitHubIssueSnapshotDigest` are the issue
  snapshot that the reconciliation manifest records, with its digest.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Repositories.GitHub`. C# namespace: `PandaBear.Repositories.GitHub`. Assembly/project identity: `SharedKernel.Repositories.GitHub`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
