# SharedKernel.ApiVersioning.AspNetCore

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

ASP.NET Core adapters for `SharedKernel.ApiVersioning` contract versions.

Route helpers use `/api/v1` style prefixes. API versions remain independent from application, assembly, and NuGet package versions.

The source checkout contains deeper policy in `docs/API_VERSIONING.md`. See the [API versioning policy](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/docs/API_VERSIONING.md).

## Usage

Use `ApiVersionRouteGroupExtensions` when mapping ASP.NET Core route groups so their metadata describes the owning API contract version. Supply the consumer-owned route prefix and lifecycle definitions; this adapter does not choose business API routes or deployment policy.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.ApiVersioning.AspNetCore`. C# namespace: `PandaBear.ApiVersioning.AspNetCore`. Assembly/project identity: `SharedKernel.ApiVersioning.AspNetCore`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
