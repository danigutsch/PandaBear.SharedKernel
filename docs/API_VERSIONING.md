# API contract versioning

API versions describe public HTTP contracts independently of assembly, application, and `PandaBear.SharedKernel.*` package versions. `PandaBear.ApiVersioning` owns host-agnostic version, lifecycle, deprecation, and selection primitives. `PandaBear.ApiVersioning.AspNetCore` owns generic route metadata and composition helpers; `PandaBear.OpenApi` owns document registration. Projects and assemblies retain their `SharedKernel.*` names.

Consumers own API prefixes, route segments, business compatibility rules, lifecycle promises, supported versions, and migration schedules. Libraries must not impose a particular host's routes or lifecycle. `ApiVersionStatus.Deprecated` keeps a version selectable while signaling migration; retirement stops version selection. Review the owning module's public API and tests for exact contracts.
