# SharedKernel C# namespaces

Date: 2026-11-24. Status: accepted.

C# namespaces return from `PandaBear.*` to `SharedKernel.*`, reversing the [PandaBear namespace decision](20261020-pandabear-namespaces.md):
`PandaBear` becomes `SharedKernel` and `PandaBear.X` becomes `SharedKernel.X`, in source, generated code, analyzer and generator metadata
names, embedded resource names, public API baselines and documentation.

A namespace follows its project and assembly name, `SharedKernel.<Project>`. The existing namespace structure stays: the `*.Abstractions`
projects share their parent's namespace, as Microsoft.Extensions does, `SharedKernel.Testing.Xunit` declares `SharedKernel.Testing`,
`SharedKernel.UploadSanitizing` declares `SharedKernel.UploadSanitizing.Images`, and `SharedKernel.Vault` declares `SharedKernel.Vault.Merge`.

One name for namespace, project and assembly removes the distinction the earlier decision introduced. Telemetry names and runtime
identifiers already use the `SharedKernel` prefix, so log categories, which are type names, now match them. Tooling no longer has to map an
assembly name to a different namespace.

Package IDs keep `PandaBear.SharedKernel.*`, including the lower-case `pandabear.sharedkernel.*` forms, because a registry needs an ID
that no other publisher owns. Assembly and project names, tool command names, the `PandaBear` company metadata and the repository name
stay unchanged.

This is a source-breaking change. Consumers adopt it in one step: replace `using PandaBear.` with `using SharedKernel.` and
`global::PandaBear.` with `global::SharedKernel.`, and update any other qualified name. Public API baselines record the change, the API
compatibility and breaking-change gates stay enabled, and the Conventional Commit history marks it as breaking. A repository convention
test rejects a `PandaBear` namespace in `src/`.

Data-protection purposes in `SharedKernel.AspNetCore.Authentication.Bff` were derived from the namespace and change with it, so payloads
protected under the old purposes, such as cached user tokens and server-side tickets, no longer unprotect; affected users sign in again.
