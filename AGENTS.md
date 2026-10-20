# SharedKernel agent instructions

Work in repository-local `.worktrees/` worktrees. Preserve public APIs, target frameworks, warnings-as-errors, and the
Microsoft.Testing.Platform runner. Use file-scoped namespaces, nullable reference types, four-space indentation, one type per file, and XML
documentation on public APIs. Do not add the Async suffix to methods.

Read the touched flow before editing. Prove production audit gaps with failing regression tests before changing behavior. Keep libraries host-agnostic
and dependency-light. Keep application compatibility in the application repository. Distribution remains private; do not publish packages. Package IDs use PandaBear.SharedKernel.*; assembly and project names remain SharedKernel.*; C# namespaces use PandaBear.*.

Restore with `dotnet restore SharedKernel.slnx --locked-mode`, build with `dotnet build SharedKernel.slnx -c Release --no-restore`, and test with
`dotnet test --solution SharedKernel.slnx -c Release --no-build --no-restore`. Use MTP filters for local debugging; CI selects complete projects. Do
not cap parallel test modules. Use Conventional Commits.

Read docs/development.md and docs/README.md. Run formatting and generated-diagram drift checks before review. Git publication requires the verified private destination; registry publication, stable tags, and consumer adoption remain deferred. Sign commits with the existing user configuration.
