# SharedKernel.Aspire.Hosting.Umami

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Aspire hosting integration for a self-hosted [Umami](https://umami.is) web analytics container with its own PostgreSQL database, and
the websites in it. Pair it with [`SharedKernel.AspNetCore.WebAnalytics`](../SharedKernel.AspNetCore.WebAnalytics/README.md), which
sends page views from the server, so the site needs no tracking script.

## Usage

```csharp
var umami = builder.AddUmami("umami");
var website = umami.AddWebsite("site", "example.com");

builder.AddProject<Projects.Site>("site-web")
    .WithReference(website, connectionName: "umami");
```

The site receives `ConnectionStrings__umami=Endpoint=<Umami's address>;WebsiteId=<id>` and adds the Umami target with
`builder.AddPageViews().AddUmami()`. Umami is optional for the site: without the reference the connection string is absent and the
site records no page views. The site does not need `WaitFor(website)`: page views sent before the website exists are refused by
Umami, and the site drops and counts them, so a broken Umami never holds the site back. Reference the website, not `umami` itself:
Umami's own connection string is its address alone, which the page-view target refuses.

`AddUmami(name)` adds:

- the container `docker.umami.is/umami-software/umami:3.4.1`, pinned by the digest of its multi-platform index
  (`sha256:e69d1a7932e9dab0fc523edc7be1f3a53f3c261f57856fc2167ae74a8fc022e8`, released 2026-10-23), with the HTTP endpoint `http`
  (container port 3000) for the dashboard and the collection API, and a health check of `/api/heartbeat`;
- the PostgreSQL server `{name}-postgres` (the pinned image of `PostgresContainerImageTags` in `PandaBear.SharedKernel.Aspire.Hosting`)
  and its database `umami` as the resource `{name}-db`; Umami waits until it is healthy and gets it as `DATABASE_URL`;
- the secret parameter `{name}-app-secret`, generated (64 letters and digits) and persisted in the AppHost's user secrets, as
  `APP_SECRET`, which signs Umami's login and cache tokens. Set `Parameters:{name}-app-secret` to use your own;
- `CLIENT_IP_HEADER=X-Forwarded-For`, so Umami takes the visitor's address from the header the page-view target sends;
- `HOSTNAME=0.0.0.0`, because some runtimes (Podman pods) replace `HOSTNAME`, which Umami's server binds to;
- `DISABLE_TELEMETRY=1`, so Umami sends no usage report of its own.

| Method | Effect |
| --- | --- |
| `AddWebsite(name, domain)` | A website in Umami, shown under it, whose connection string a site references; it is created in Umami when Umami is ready. |
| `WithWebsiteId(id)` | Uses this website id instead of the name-based one, for a website that exists with another id. |
| `WithAdminPassword(parameter)` | The `admin` password to sign in with, for a kept Umami whose password was changed. |
| `WithDataVolume(name?)` | Keeps the analytics data in a named volume of the PostgreSQL server. The Umami container itself keeps no state. |
| `WithDataBindMount(source)` | Keeps the analytics data in a host folder instead. |
| `WithHostPort(port)` | A fixed host port for the dashboard, so its address can be bookmarked. |

`UmamiResource.Database` (with its `Parent` server) and `UmamiResource.AppSecretParameter` are there for further configuration, such as
a PostgreSQL password parameter or pgAdmin.

## Nothing to set up

Under the AppHost there is nothing to do in Umami: no first login, no website to add by hand, no id to copy. When Umami is ready (its
health check passes), each website resource signs in as `admin` with Umami's built-in default password `umami`, which a fresh
database has, and creates its website with its id unless a website with that id exists. The website resource shows it in the
dashboard: `Waiting` until Umami is ready, `Starting` while it is set up, then `Running` with its id and domain as properties, or
`FailedToStart` with a log line that names the request and Umami's status code. It runs in the background, so a failure never blocks
or fails another resource, and no log or state shows the password or the token.

The website id is a name-based UUID (RFC 9562, version 5) of the Umami resource name, the website name and the domain. It is the same
in every run without storing anything, so a kept database (`WithDataVolume`) is found again and nothing is created twice.

To look at the statistics, open the `umami` resource's `http` endpoint from the dashboard and sign in as `admin` / `umami`. The default
password is fine for a throwaway container that only the developer's machine reaches. For a Umami whose data is kept for long, change
the password in Umami (3.4: profile button, Settings, Profile, Change password) and pass the new one, so the website can still be set up:

```csharp
var umamiPassword = builder.AddParameter("umami-admin-password", secret: true);
var umami = builder.AddUmami("umami").WithDataVolume().WithAdminPassword(umamiPassword);
```

Setting up uses three routes of Umami 3.4.1, checked against its source: `POST /api/auth/login` (`src/app/api/auth/login/route.ts`),
`GET /api/websites/{id}` (`src/app/api/websites/[websiteId]/route.ts`, which answers `null` for an unknown website) and
`POST /api/websites` with `id`, `name` and `domain` (`src/app/api/websites/route.ts`). An `admin` with two-factor sign-in cannot be
used.

## Lifetime and persistence

Umami follows [Aspire's resource lifetimes](https://aspire.dev/app-host/resource-lifetimes/): by default each resource has the
session lifetime, created when the AppHost starts and disposed of when it stops, so Umami, its PostgreSQL server and every page view
recorded during a run are discarded with the run, and the next run starts from a fresh database. That is the right default for
development and tests.

When the data must survive the AppHost, call `WithDataVolume()`, Aspire's way to keep a database's data: the PostgreSQL data folder then
lives in a named volume, and the next run finds the same Umami, the same users and the same website. `WithDataBindMount(source)` keeps
it in a host folder instead. The generated secrets follow Aspire's own pattern for generated values, as `AddPostgres` does: in run mode
`{name}-app-secret` is saved to the AppHost's user secrets, so it stays stable across runs and Umami's tokens stay valid with a kept
database; the PostgreSQL password parameter of `{name}-postgres` is kept the same way.

## Publish

In publish mode nothing is set up: the website resource carries only its connection string. A deployment creates the website in its
Umami (the dashboard's Add website, or `POST /api/websites` with the same id), changes the default `admin` password, keeps the
PostgreSQL data on a durable volume with backups, and gives the site `ConnectionStrings__umami=Endpoint=<Umami's public or internal
address>;WebsiteId=<id>`. Pass the id with `WithWebsiteId` when the website exists with another one.

## Pinned image

Umami 3.4.1 is pinned because it was the newest release at least two weeks old when the pin was chosen; 3.5.0 (2026-11-12) was three
days old. 3.5.0 requires `APP_SECRET`, which this resource already sets, forces the first password change, and adds
`TRUSTED_PROXIES`; review its migrations and re-check the setup routes before moving the pin. Umami supports PostgreSQL 12.14 and later.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Aspire.Hosting.Umami`. C# namespaces: `Aspire.Hosting` (extensions) and
`Aspire.Hosting.ApplicationModel` (`UmamiResource`, `UmamiWebsiteResource`). Assembly/project identity:
`SharedKernel.Aspire.Hosting.Umami`. It depends on `Aspire.Hosting`, `Aspire.Hosting.PostgreSQL` and
`PandaBear.SharedKernel.Aspire.Hosting`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private
feed. This README does not imply availability on nuget.org.
