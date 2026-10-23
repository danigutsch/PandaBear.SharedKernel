# PandaBear.SharedKernel

[![Build](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Reusable .NET building blocks for services and libraries: explicit results, generated mediation, typed HTTP outcomes, persistence integrations, and testing helpers.

**Preview:** APIs may change. Packages are not published to NuGet.org yet; use a validated preview candidate from a [successful build](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml).

## Find the right package

Choose the packages your project needs. Each package has its own README with requirements and examples.

| What you need | Start here |
| --- | --- |
| Represent success, validation failures, and optional values | [Results](src/SharedKernel/SharedKernel.Results/README.md) · [Functional](src/SharedKernel/SharedKernel.Functional/README.md) |
| Model domains and dispatch requests or events | [Domain](src/SharedKernel/SharedKernel.Domain/README.md) · [Mediator](src/SharedKernel/SharedKernel.Mediator/README.md) |
| Handle HTTP outcomes, uploads, caching, and API versions | [HttpClients](src/SharedKernel/SharedKernel.HttpClients/README.md) · [HTTP caching](src/SharedKernel/SharedKernel.HttpCaching.AspNetCore/README.md) · [API versioning](src/SharedKernel/SharedKernel.ApiVersioning/README.md) |
| Add database-specific persistence and coordination | [Entity Framework Core](src/SharedKernel/SharedKernel.EntityFrameworkCore/README.md) · [PostgreSQL](src/SharedKernel/SharedKernel.Npgsql/README.md) · [Idempotency](src/SharedKernel/SharedKernel.Idempotency.Abstractions/README.md) |
| Work with durable events and integration messages | [Event sourcing](src/SharedKernel/SharedKernel.EventSourcing/README.md) · [Integration events](src/SharedKernel/SharedKernel.Messaging.IntegrationEvents/README.md) |
| Rewind bounded uploads or imports for multiple passes | [IO](src/SharedKernel/SharedKernel.IO/README.md) |
| Parse simple comma-delimited rows and detect duplicates | [Tabular](src/SharedKernel/SharedKernel.Tabular/README.md) |
| Build focused tests and container-backed integration tests | [Testing](src/SharedKernel/SharedKernel.Testing/README.md) · [Integration testing](src/SharedKernel/SharedKernel.IntegrationTesting/README.md) |
| Automate documentation and package maintenance | [Documentation tool](tools/SharedKernel.Documentation.Tool/README.md) · [Versioning tool](tools/SharedKernel.Versioning.Tool/README.md) |

## Try it

Download the unchanged `.nupkg` files and `candidate.json` from the `candidate-packages` artifact of a successful CI run on `main` into a local feed directory. NuGet registry publication and stable promotion remain deferred. Verify the clean source checkout matches the manifest revision and every package SHA-256 matches the trusted candidate manifest before consumption. Replace the placeholders below with that candidate's version and directory:

```text
dotnet add package PandaBear.SharedKernel.Results --version <preview-version> --source <candidate-feed>
```

Make expected outcomes explicit:

```csharp
using PandaBear.Results;

public static class Example
{
    public static Result<int> ParseCount(string text)
        => int.TryParse(text, out var value) && value >= 0
            ? Result.Ok(value)
            : Result.Invalid<int>("Count is invalid.", "count", "Use a non-negative integer.");
}
```

Package IDs start with `PandaBear.SharedKernel.`; C# namespaces start with `PandaBear.`. The [Results sample](samples/Results/BasicResults.Sample/README.md) shows composition, optional values, and asynchronous flows.

## Build from source

Install the .NET SDK pinned in [global.json](global.json), then clone and build:

```sh
git clone https://github.com/danigutsch/PandaBear.SharedKernel.git
cd PandaBear.SharedKernel
bash setup-dev.sh
```

On Windows, run `pwsh -File setup-dev.ps1` instead. Run the tests with:

```sh
dotnet test --solution SharedKernel.slnx -c Release --no-build --no-restore
```

Provider integration tests need Docker or a compatible Podman runtime. NativeAOT checks also need the platform's native build tools; see [development prerequisites](docs/development.md).

The libraries favor typed APIs and existing source generators over runtime reflection where practical. AOT and trimming support depend on the package and its providers; check the package README for its supported boundary.

## Learn more

- [Documentation and architecture](docs/README.md)
- [Package development, coverage, and validation](docs/package-development.md)
- [Preview versions and release policy](docs/release-policy.md)
- [Contributing](CONTRIBUTING.md) · [Security](SECURITY.md) · [License](LICENSE.txt)
