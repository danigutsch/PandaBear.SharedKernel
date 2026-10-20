# SharedKernel.ApiVersioning

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Host-agnostic API contract versioning primitives for SharedKernel packages.

API versions describe public HTTP contracts. They do not follow application, assembly, or NuGet package versions.

The source checkout contains deeper policy in `docs/API_VERSIONING.md`. See the [API versioning policy](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/docs/API_VERSIONING.md).

## Usage

Use `ApiVersion` and `ApiVersionDefinition` to declare a public HTTP contract version, and `ApiVersionCatalog` to select supported versions. Configure lifecycle and sunset/migration information using `ApiVersionStatus` and `ApiDeprecationPolicy`. The consuming host owns version support promises and request routing.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.ApiVersioning`. C# namespace: `PandaBear.ApiVersioning`. Assembly/project identity: `SharedKernel.ApiVersioning`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
