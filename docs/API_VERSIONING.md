# API contract versioning

API versions describe public HTTP contracts independently of assembly, application, and `PandaBear.SharedKernel.*` package versions. `PandaBear.ApiVersioning` owns host-agnostic version, lifecycle, deprecation, and selection primitives. `PandaBear.ApiVersioning.AspNetCore` owns generic route metadata and composition helpers; `PandaBear.OpenApi` owns document registration. Projects and assemblies retain their `SharedKernel.*` names.

Consumers own API prefixes, route segments, business compatibility rules, alpha/beta promises, supported versions, and migration schedules. Libraries must not impose ViajantesTurismo's `/api/v1` routes or application lifecycle. `ApiVersionStatus.Deprecated` keeps a version selectable while signaling migration; retirement stops version selection. Review the owning module's public API and tests for exact contracts.

Adapted from the library-owned portions of ViajantesTurismo `docs/API_VERSIONING.md` at commit `9116ad887459172c4ad15a65259ffe587bcbb902`. Application route inventory, conditional update semantics, alpha compatibility policy, deployment, and speculative generation plans remain consumer-owned.
