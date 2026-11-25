# PandaBear.SharedKernel.CI.GitHub

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

The GitHub provider of `PandaBear.SharedKernel.CI`: GitHub Actions step outputs and job summaries, and the GitHub
releases that prerelease pruning and candidate staging use. It builds on `PandaBear.SharedKernel.GitHub` for the token
and the REST headers.

```csharp
using System.Net.Http.Headers;
using SharedKernel.CI;
using SharedKernel.CI.GitHub;
using SharedKernel.GitHub;

using var releases = new GitHubReleaseStore(
    new SocketsHttpHandler { AllowAutoRedirect = false },
    "owner/name",
    new ProductInfoHeaderValue("sharedkernel-ci", "1.0"),
    GitHubToken.Find(Environment.GetEnvironmentVariable));
await PrereleasePruner.Prune(new PrereleasePruningRequest("owner/name", 20, [], Force: false), releases, Console.Out, Console.Error, ct);
```

- `GitHubActionsOutputs(path)` appends one `name=value` line per output to the `GITHUB_OUTPUT` file, as
  [Setting an output parameter](https://docs.github.com/actions/reference/workflows-and-actions/workflow-commands#setting-an-output-parameter)
  describes. A name with `=` or a line break, or a value with a line break, is refused rather than written in the
  multi-line delimiter form.
- `GitHubActionsStepSummary(path)` appends Markdown to the `GITHUB_STEP_SUMMARY` file, as
  [Adding a job summary](https://docs.github.com/actions/reference/workflows-and-actions/workflow-commands#adding-a-job-summary)
  describes; GitHub limits a step to 1 MiB.
- `GitHubReleaseStore` lists the releases of a repository 100 per page through
  [the REST releases API](https://docs.github.com/rest/releases/releases), reads every page before anything is
  deleted, bounds each page to 16 MiB and each request to one minute, and deletes a release with
  `DELETE /repos/{owner}/{repo}/releases/{id}`, which keeps its tag. It owns the handler it is given; pass one that
  refuses redirects, so the token never reaches another address.
- `GitHubReleaseAssets` returns `https://github.com/{owner}/{repo}/releases/download/{tag}/{asset}` without a request;
  the engine downloads it without credentials, following at most five redirects, each only to HTTPS.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.CI.GitHub`. C# namespace: `SharedKernel.CI.GitHub`. Assembly/project identity: `SharedKernel.CI.GitHub`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
