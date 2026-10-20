# SharedKernel.ApiVersioning

Host-agnostic API contract versioning primitives for SharedKernel packages.

API versions describe public HTTP contracts. They do not follow application, assembly, or NuGet package versions.

The source checkout contains deeper policy in `docs/API_VERSIONING.md`. A canonical hosted documentation URL will be added after the private repository destination is verified.

## Usage

Use `ApiVersion` and `ApiVersionDefinition` to declare a public HTTP contract version, and `ApiVersionCatalog` to select supported versions. Configure lifecycle and sunset/migration information using `ApiVersionStatus` and `ApiDeprecationPolicy`. The consuming host owns version support promises and request routing.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.ApiVersioning`. C# namespace: `PandaBear.ApiVersioning`. Assembly/project identity: `SharedKernel.ApiVersioning`.

Consume a validated private candidate at an explicitly selected version. Registry publication is deferred; no public NuGet availability is implied.
