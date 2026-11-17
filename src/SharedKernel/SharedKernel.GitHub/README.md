# PandaBear.SharedKernel.GitHub

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

GitHub API access for command-line tools and their engines: one place that knows where the token comes from, keeps it
away from child processes, and sets the headers the REST API expects. Targets .NET 10, references only
`PandaBear.SharedKernel.Processes`, and is trimming- and NativeAOT-compatible.

```csharp
using System.Net.Http.Headers;
using PandaBear.GitHub;

var token = GitHubToken.Find(Environment.GetEnvironmentVariable)
    ?? throw new InvalidOperationException("Set GH_TOKEN or GITHUB_TOKEN before syncing labels.");
using var client = GitHubApiClient.Create(
    new HttpClientHandler { AllowAutoRedirect = false },
    new ProductInfoHeaderValue("sharedkernel-repo", "1.0"),
    token);
```

- `GitHubToken.Find` returns the first of `GH_TOKEN` and `GITHUB_TOKEN` that is not empty or white-space, the order
  [`gh`](https://cli.github.com/manual/gh_help_environment) uses, or `null`. The caller decides whether a missing token
  is an error and words the message; an engine takes the reader as a `Func<string, string?>`, so tests pass a fake.
  `GitHubToken.VariableNames` lists only those two: a GitHub Enterprise Server token (`GH_ENTERPRISE_TOKEN`,
  `GITHUB_ENTERPRISE_TOKEN`) is never read, so it is never sent to `api.github.com`.
- `GitHubToken.RemoveFrom(startInfo)` removes every forge token variable, in any case, from a child process
  environment: both variables above, the Enterprise Server ones, and the rest of
  `ProcessEnvironment.ForgeTokenVariableNames`. `IsTokenVariable` reports the same names, so a configuration reader
  refuses a request to forward any of them. A token is never an argument.
- `GitHubApiClient.Create(handler, userAgent, token)` returns a client that owns the handler and sends the user agent,
  `Accept: application/vnd.github+json`, `X-GitHub-Api-Version: 2026-03-10`, and the bearer token when one is given. It
  keeps the default `HttpClient.Timeout`; a caller that times each request with its own token sets
  `Timeout.InfiniteTimeSpan`. Redirect and certificate policy belong to the handler the caller passes.

Bounded response reading is `PandaBear.IO.BoundedReader.ReadToEnd`.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.GitHub`. C# namespace: `PandaBear.GitHub`. Assembly/project identity: `SharedKernel.GitHub`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
