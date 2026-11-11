# SharedKernel.Aspire.Hosting

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

AppHost helpers that are not tied to one container. The package depends on `Aspire.Hosting` (and SharedKernel.AspNetCore for the
canonical `ASPNETCORE_ENVIRONMENT` name) only; the PostgreSQL and Redis pins are constants, so they do not bring
`Aspire.Hosting.PostgreSQL` or `Aspire.Hosting.Redis` into an AppHost that does not use them.

```csharp
using PandaBear.Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var signingKey = builder.AddGeneratedSecret("signing-key");

var postgres = builder.AddPostgres("postgres")
    .WithImageTag(PostgresContainerImageTags.Tag)
    .WithImageSHA256(PostgresContainerImageTags.Digest);

var site = builder.AddProject<Projects.Site>("site")
    .WithEnvironment("Signing__Key", signingKey)
    .WithHttpHealthCheckInAppHostEnvironment()
    .WithSelfOrigin("PublicSite__Origin");
```

## API

| API | What it does |
| --- | --- |
| `AddGeneratedSecret(name, minLength = 32)` | A secret parameter whose value is generated (letters and digits, at least 16 long) unless `Parameters:{name}` is configured, for example in user secrets. |
| `WithSelfOrigin(environmentVariable, endpointName = null)` | Gives the resource the URL of its own endpoint: the named one, or `https`, else `http`. |
| `WithHttpHealthCheckInAppHostEnvironment(path = "/health", onlyInDevelopment = false)` | Sets `ASPNETCORE_ENVIRONMENT` to the AppHost environment and adds `WithHttpHealthCheck(path)` exactly when the project maps it. |
| `PostgresContainerImageTags`, `RedisContainerImageTags` | `Registry`, `Image`, `Tag` and `Digest` of a pinned `docker.io/library/postgres:18.4` and `docker.io/library/redis:8.8`. |

The extension methods live in the `Aspire.Hosting` namespace, as Aspire hosting integrations and the other SharedKernel hosting packages
do, so an AppHost finds them without a `using`. The pins live in `PandaBear.Aspire.Hosting`.

## Decisions

- **Secrets are parameters.** Following the Aspire rule to model API keys, tokens and passwords as secret AppHost parameters, not `.env`
  values. A generated value is not saved, so it changes every run; configure it when a persistent volume already holds the old value.
- **Never a hard-coded URL.** Following the Aspire rule to pass endpoint references to `WithEnvironment`, `WithSelfOrigin` passes the
  resource's own endpoint reference: the allocated address at run time, an endpoint expression in a manifest. Without a name it prefers
  `https` and falls back to `http`, so the same AppHost runs where only HTTP exists; a name you give must exist, or the resource fails to
  start with an error that names it. A container receives its container-network address, which a browser cannot reach.
- **The check matches the endpoint.** Following the Aspire rule that a resource others `WaitFor` needs `WithHttpHealthCheck("/health")`
  served by the service defaults. The project runs in the AppHost's environment rather than its launch profile's, and with
  `onlyInDevelopment: true` (for `HealthEndpointExposure.DevelopmentOnly` in [SharedKernel.ServiceDefaults](../SharedKernel.ServiceDefaults/README.md))
  the check is added only in Development. A check against a missing endpoint never turns healthy and blocks every `WaitFor`.
- **Pins.** `WithImageSHA256` replaces the tag in the image reference; `Tag` names the version the digest belongs to. The digests come from
  the ViajantesTurismo AppHost; bump tag and digest together when you update.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Aspire.Hosting`. Assembly/project identity: `SharedKernel.Aspire.Hosting`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
