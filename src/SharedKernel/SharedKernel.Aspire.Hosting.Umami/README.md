# SharedKernel.Aspire.Hosting.Umami

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Aspire hosting integration for a self-hosted [Umami](https://umami.is) web analytics container with its own PostgreSQL database.
Pair it with [`SharedKernel.AspNetCore.WebAnalytics`](../SharedKernel.AspNetCore.WebAnalytics/README.md), which sends page views
from the server, so the site needs no tracking script.

## Usage

```csharp
var umami = builder.AddUmami("umami")
    .WithDataVolume();

builder.AddProject<Projects.Site>("site")
    .WithReference(umami);
```

The site receives Umami's base address as `ConnectionStrings__umami` and registers the forwarder with
`AddUmamiPageViews<TWebsiteIdProvider>("ConnectionStrings:umami")`. It does not need `WaitFor(umami)`: page views wait in the
forwarder's queue, and the ones sent before Umami answers are dropped and counted, so a broken Umami never holds the site back.

`AddUmami(name)` adds:

- the container `docker.umami.is/umami-software/umami:3.4.1`, pinned by the digest of its multi-platform index
  (`sha256:e69d1a7932e9dab0fc523edc7be1f3a53f3c261f57856fc2167ae74a8fc022e8`, released 2026-10-23), with the HTTP endpoint `http`
  (container port 3000) for the dashboard and the collection API, and a health check of `/api/heartbeat`;
- the PostgreSQL server `{name}-postgres` (the pinned image of `PostgresContainerImageTags` in `PandaBear.SharedKernel.Aspire.Hosting`)
  and its database `umami` as the resource `{name}-db`; Umami waits until it is healthy and gets it as `DATABASE_URL`;
- the secret parameter `{name}-app-secret`, generated (64 letters and digits) and persisted in the AppHost's user secrets, as
  `APP_SECRET`, which signs Umami's login and cache tokens. Set `Parameters:{name}-app-secret` to use your own;
- `CLIENT_IP_HEADER=X-Forwarded-For`, so Umami takes the visitor's address from the header the forwarder sends;
- `HOSTNAME=0.0.0.0`, because some runtimes (Podman pods) replace `HOSTNAME`, which Umami's server binds to;
- `DISABLE_TELEMETRY=1`, so Umami sends no usage report of its own.

| Method | Effect |
| --- | --- |
| `WithDataVolume(name?)` | Keeps the analytics data in a named volume of the PostgreSQL server. The Umami container itself keeps no state. |
| `WithDataBindMount(source)` | Keeps the analytics data in a host folder instead. |
| `WithHostPort(port)` | A fixed host port for the dashboard, so its address can be bookmarked. |

Without `WithDataVolume` or `WithDataBindMount` the data lives and dies with the PostgreSQL container. `UmamiResource.Database`
(with its `Parent` server) and `UmamiResource.AppSecretParameter` are there for further configuration, such as a PostgreSQL password
parameter or pgAdmin.

## First login

1. Open the `umami` resource's `http` endpoint from the Aspire dashboard and sign in as `admin` with the password `umami`.
2. Change that password at once: in 3.4 Umami does not force it; it is under the profile button, Settings, Profile, Change password
   (3.5 forces the change and moves it to Security).
3. Click Websites in the sidebar, then Add website, with the site's public domain, such as `example.com`; Umami uses the domain to keep
   the site's own pages out of the referrers.
4. Copy the website's ID (a GUID, shown in its settings and in its tracking code) to where the site's `IUmamiWebsiteIdProvider` reads
   it, for example the site owner's settings. The site needs no tracking code.

Until the site has a website id it records nothing.

## Pinned image

Umami 3.4.1 is pinned because it was the newest release at least two weeks old when the pin was chosen; 3.5.0 (2026-11-12) was three
days old. 3.5.0 requires `APP_SECRET`, which this resource already sets, forces the first password change, and adds
`TRUSTED_PROXIES`; review its migrations before moving the pin. Umami supports PostgreSQL 12.14 and later.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Aspire.Hosting.Umami`. C# namespaces: `Aspire.Hosting` (extensions) and
`Aspire.Hosting.ApplicationModel` (`UmamiResource`). Assembly/project identity: `SharedKernel.Aspire.Hosting.Umami`. It depends on
`Aspire.Hosting`, `Aspire.Hosting.PostgreSQL` and `PandaBear.SharedKernel.Aspire.Hosting`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private
feed. This README does not imply availability on nuget.org.
