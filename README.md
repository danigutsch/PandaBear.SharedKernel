# SharedKernel

Private reusable .NET libraries extracted from ViajantesTurismo. Package IDs and public APIs remain unchanged. The extraction manifest, history
mapping, and archived extraction tools are in `provenance/`.

## Development

Install the SDK pinned in `global.json`. Provider integration tests require a working container runtime; NativeAOT checks also require the platform
native build toolchain.

Analyzer and source-generator packages retain the Roslyn 5.6 compiler API baseline so consumers can use compatible .NET 10 SDKs without upgrading
to this repository's development SDK. The repository SDK and runtime dependencies can advance independently of that compiler compatibility floor.

```sh
dotnet restore SharedKernel.slnx --locked-mode
dotnet build SharedKernel.slnx -c Release --no-restore
dotnet test --solution SharedKernel.slnx -c Release --no-build --no-restore
```

Ongoing automation uses C# tools. `SharedKernel.Repository.Tool` is repository-only and is not packaged. Selection reads the live solution, includes
reverse transitive dependents and whole test projects, and falls back to full validation when history or dependencies are uncertain. Full runs happen
nightly; selection reasons are retained in CI.

```sh
dotnet run --project tools/SharedKernel.Repository.Tool -c Release -- select --base <base-revision>
dotnet run --project tools/SharedKernel.Repository.Tool -c Release -- validate --pack --aot
```

Commit changes before creating candidate evidence. Candidates receive unique ordered prerelease versions and are never overwritten.
`SharedKernel.Versioning.Tool` validates APIs, packs once, records the library revision and SHA-256 package hashes, then checks isolated package
restore, locked restore, build, run, generators, and optional NativeAOT. Candidate manifests mark unavailable consumer, baseline, and platform
evidence as pending.

```sh
dotnet run --project tools/SharedKernel.Versioning.Tool -c Release -- validate-candidate --aot
dotnet run --project tools/SharedKernel.Versioning.Tool -c Release -- consume-candidate --candidate <candidate.json> --aot
```

Candidate packages and their manifest can be copied together to another machine; consumption regenerates configuration for the current feed location.
Linux and Windows consume the same hashed artifacts before stable promotion. Source Link remains pending until a repository remote is configured.
Transitional application test friend assemblies remain until a separate consumer cutover removes their need.

## Consume changes in ViajantesTurismo

Copy candidate `.nupkg` files into the application's ignored `.local/sharedkernel-feed`. Set the SharedKernel versions in `Directory.Packages.props`,
restore to update committed lock files, and review the resulting changes in an application worktree. Internal packages map exclusively to that feed.
Ordinary dependency declarations use minimum compatible versions without upper bounds; exact artifact versions are used only in package-consumption
checks. Locked CI restores make adoption deliberate without preventing future upgrades.

Run application-owned compatibility against the candidate manifest:

```sh
dotnet run --project tools/SharedKernel.RepoConfig.Tool -c Release -- verify-sharedkernel-candidate --candidate <candidate.json> --output <compatibility.json>
```

Compatibility records both repository revisions and package hashes. Failures block application adoption and stable promotion, independently of library
merge. Keep the previous application dependency configuration for rollback. Promote the same tested artifacts; no public publishing, dependency
replacements, or application cutover are part of this setup.
