# SharedKernel.ApiVersioning.AspNetCore

ASP.NET Core adapters for `SharedKernel.ApiVersioning` contract versions.

Route helpers use `/api/v1` style prefixes. API versions remain independent from application, assembly, and NuGet package versions.

The source checkout contains deeper policy in `docs/API_VERSIONING.md`. A canonical hosted documentation URL will be added after the private repository destination is verified.

## Usage

Use `ApiVersionRouteGroupExtensions` when mapping ASP.NET Core route groups so their metadata describes the owning API contract version. Supply the consumer-owned route prefix and lifecycle definitions; this adapter does not choose business API routes or deployment policy.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.ApiVersioning.AspNetCore`. C# namespace: `PandaBear.ApiVersioning.AspNetCore`. Assembly/project identity: `SharedKernel.ApiVersioning.AspNetCore`.

Consume a validated private candidate at an explicitly selected version. Registry publication is deferred; no public NuGet availability is implied.
