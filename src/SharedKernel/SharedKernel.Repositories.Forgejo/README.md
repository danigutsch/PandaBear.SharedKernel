# PandaBear.SharedKernel.Repositories.Forgejo

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

The Forgejo provider of `PandaBear.SharedKernel.Repositories`: it implements the `IRepositoryLabelStore` seam, so
`RepositoryLabelSync` keeps the labels of a Forgejo repository in line with the consumer configuration. It builds on
`PandaBear.SharedKernel.Forgejo` for the token, the API base address, and the headers, and does not reference GitHub.

```csharp
using System.Net.Http.Headers;
using PandaBear.Forgejo;
using PandaBear.Repositories;
using PandaBear.Repositories.Forgejo;

using var labels = new ForgejoLabelStore(
    new SocketsHttpHandler { AllowAutoRedirect = false },
    new Uri("https://codeberg.org"),
    "owner/name",
    new ProductInfoHeaderValue("sharedkernel-repo", "1.0"),
    ForgejoToken.Find(Environment.GetEnvironmentVariable));
await RepositoryLabelSync.Run(repositoryRoot, labels, force: false, Console.Out, ct);
```

Checked on 2026-11-15 against the [API usage](https://forgejo.org/docs/latest/user/api-usage/) documentation and the
API description of Forgejo 17 (`swagger.v1.json`, [reference](https://code.forgejo.org/api/swagger)):

- `List` reads `GET /api/v1/repos/{owner}/{repo}/labels` with `limit=50` and `page` through `ForgejoPager` of
  `PandaBear.SharedKernel.Forgejo`, and fails above 100 pages. Forgejo 17.0.1 answers the label list with
  `X-Total-Count` and no `Link` header, so the total is what ends the listing. Forgejo returns colors as `rrggbb`; a leading `#` is removed in case an
  instance returns one.
- `Create` sends `POST /api/v1/repos/{owner}/{repo}/labels` (`CreateLabelOption`: `name`, `color` as `#rrggbb`,
  `description`) and expects 201 Created.
- `Update` sends `PATCH /api/v1/repos/{owner}/{repo}/labels/{id}` (`EditLabelOption`) and expects 200 OK. Forgejo edits
  a label by its numeric ID, so the store uses the ID that the last `List` read for the label's current name. Fields
  the configuration does not hold, such as `exclusive` and `is_archived`, are not sent and stay as they are.
- Each request times out after 30 seconds and reads at most 1 MiB. Errors report the status code and never echo a
  response body or transport message. Labels are never deleted.

Reading the labels of a public repository works without a token. Writing needs a token; the
[token scope](https://forgejo.org/docs/latest/user/token-scope/) documentation lists label routes under the `issue`
category.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Repositories.Forgejo`. C# namespace: `PandaBear.Repositories.Forgejo`. Assembly/project identity: `SharedKernel.Repositories.Forgejo`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
