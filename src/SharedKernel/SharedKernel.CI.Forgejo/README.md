# PandaBear.SharedKernel.CI.Forgejo

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

The Forgejo provider of `PandaBear.SharedKernel.CI`: Forgejo Actions step outputs and step summaries, and the Forgejo
releases that prerelease pruning and candidate staging use. It builds on `PandaBear.SharedKernel.Forgejo` for the token,
the API base address, and the headers.

```csharp
using System.Net.Http.Headers;
using PandaBear.CI;
using PandaBear.CI.Forgejo;
using PandaBear.Forgejo;

using var releases = new ForgejoReleaseStore(
    new SocketsHttpHandler { AllowAutoRedirect = false },
    new Uri("https://codeberg.org"),
    "owner/name",
    new ProductInfoHeaderValue("sharedkernel-ci", "1.0"),
    ForgejoToken.Find(Environment.GetEnvironmentVariable));
await PrereleasePruner.Prune(new PrereleasePruningRequest("owner/name", 20, [], Force: false), releases, Console.Out, Console.Error, ct);
```

Checked on 2026-11-15 against the Forgejo documentation and the API description of Forgejo 17 (`swagger.v1.json`):

- `ForgejoActionsOutputs(path)` appends one `name=value` line per output to the `FORGEJO_OUTPUT` file, which the
  [Actions reference](https://forgejo.org/docs/latest/user/actions/reference/) documents for step and job outputs; every
  `FORGEJO_*` variable is also available as `GITHUB_*`. Multi-line values are refused.
- `ForgejoActionsStepSummary(path)` appends Markdown to the `FORGEJO_STEP_SUMMARY` file, which
  [Job summaries](https://forgejo.org/docs/latest/user/actions/advanced-features/) documents; Forgejo truncates a step's
  summary beyond 1 MiB.
- `ForgejoReleaseStore` lists `GET /api/v1/repos/{owner}/{repo}/releases` with `limit=50` and `page` through
  `ForgejoPager` of `PandaBear.SharedKernel.Forgejo`, which follows the `Link` header, or `X-Total-Count` without one, and
  fails above 200 pages; it reads every page before anything is deleted, bounds each page to 16 MiB and each request to
  one minute, and deletes with `DELETE /api/v1/repos/{owner}/{repo}/releases/{id}` (204 No Content), which keeps the
  tag.
- `ForgejoReleaseAssets` reads `GET /api/v1/repos/{owner}/{repo}/releases/tags/{tag}` once per tag without credentials
  and returns the `browser_download_url` of the attachment with the requested name.

Forgejo Actions documents no workflow-command annotations (`::error::` and similar), so this package offers none; the
engine emits none either.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.CI.Forgejo`. C# namespace: `PandaBear.CI.Forgejo`. Assembly/project identity: `SharedKernel.CI.Forgejo`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
