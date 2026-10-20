# SharedKernel agent instructions

Work in repository-local `.worktrees/` worktrees. Preserve public APIs, target frameworks, warnings-as-errors, and the
Microsoft.Testing.Platform runner. Use file-scoped namespaces, nullable reference types, four-space indentation, one type per file, and XML
documentation on public APIs. Do not add the Async suffix to methods.

Read the touched flow before editing. Prove production audit gaps with failing regression tests before changing behavior. Keep libraries host-agnostic
and dependency-light. Keep application compatibility in the application repository. Git source is public at https://github.com/danigutsch/PandaBear.SharedKernel. Registry publication remains deferred; do not publish packages. Package IDs use PandaBear.SharedKernel.*; assembly and project names remain SharedKernel.*; C# namespaces use PandaBear.*.

Restore with `dotnet restore SharedKernel.slnx --locked-mode`, build with `dotnet build SharedKernel.slnx -c Release --no-restore`, and test with
`dotnet test --solution SharedKernel.slnx -c Release --no-build --no-restore`. Use MTP filters for local debugging; CI selects complete projects. Do
not cap parallel test modules. Use Conventional Commits.

Read docs/development.md and docs/README.md. Run formatting and generated-diagram drift checks before review. Git publication is authorized to the public danigutsch/PandaBear.SharedKernel repository. Registry publication, stable tags, and consumer adoption remain separate actions requiring their own authorization. Sign commits with the existing user configuration.

All packages remain preview (prerelease) for now. CI must calculate prerelease candidates only; do not create stable packages, tags, or releases without separate authorization.

Prefer NativeAOT- and trimming-compatible library APIs where practical. Prefer explicit typed code and existing source generators over runtime reflection or dynamic code. Preserve inherited AOT/trim analysis; document justified project opt-outs and reflection requirements, and validate promised support with packed consumers on each claimed platform. Do not introduce a generator or abstraction solely to satisfy this preference.
