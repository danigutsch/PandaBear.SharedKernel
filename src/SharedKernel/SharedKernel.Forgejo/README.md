# PandaBear.SharedKernel.Forgejo

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Forgejo API access for command-line tools and their engines, mirroring `PandaBear.SharedKernel.GitHub`: one place that
knows where the token comes from, keeps it away from child processes, and sets the headers and base address of the
REST API of a Forgejo instance. Targets .NET 10, references only `PandaBear.SharedKernel.Processes`, and is trimming-
and NativeAOT-compatible.

```csharp
using System.Net.Http.Headers;
using PandaBear.Forgejo;

using var client = ForgejoApiClient.Create(
    new SocketsHttpHandler { AllowAutoRedirect = false },
    new Uri("https://codeberg.org"),
    new ProductInfoHeaderValue("sharedkernel-ci", "1.0"),
    ForgejoToken.Find(Environment.GetEnvironmentVariable));
using var response = await client.GetAsync("repos/owner/name/releases?limit=50&page=1", ct);
```

- `ForgejoToken.Find` returns `FORGEJO_TOKEN` when it is not empty or white-space, or `null`. Forgejo Actions sets
  that variable to the automatic token of the run, as
  [the Actions reference](https://forgejo.org/docs/latest/user/actions/reference/) lists. `RemoveFrom` removes it, in any
  case, from a child process environment, and `IsTokenVariable` lets a configuration reader refuse to forward it.
- `ForgejoApiClient.Create(handler, server, userAgent, token)` returns a client whose base address is
  `<server>/api/v1/`, that sends the user agent, `Accept: application/json`, and `Authorization: token <token>`, the
  header form that [API usage](https://forgejo.org/docs/latest/user/api-usage/) documents. The server must be HTTPS. It
  keeps the default `HttpClient.Timeout`; redirect and certificate policy belong to the handler the caller passes.
- Forgejo pages lists with `page` and `limit`, caps `limit` at the instance's `max_response_items` (50 by default), and
  sends a `Link` header with a `next` relation while more pages follow.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Forgejo`. C# namespace: `PandaBear.Forgejo`. Assembly/project identity: `SharedKernel.Forgejo`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
