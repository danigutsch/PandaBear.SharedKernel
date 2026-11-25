# PandaBear.SharedKernel.Vault

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Offline engine for password vault exports: provider-neutral vault items, duplicate matching, field-by-field merging
with a decisions file, secret scanning and placement, briefs and catalogs, password health, and value-free reports.
The `dotnet vault` tool is a command-line wrapper over this library and its providers; its
[README](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/tools/SharedKernel.VaultManager.Tool/README.md)
describes the merge rules, limits, and security notes that this library applies to any host.

## Usage

Hosts that import both Keeper and Bitwarden files can use the [vault export import provider selector](../SharedKernel.Vault.Import/README.md).
Hosts that already know the input format can use the provider directly.

`ReviewSession` works with one already scanned vault. Use it to apply field, finding, and match
decisions, then read the current merge and its pending decisions. Its field and finding views
mask values until the caller explicitly reveals them. `Save` supplies a decisions checkpoint
to the caller's persistence callback. A new session can resume from that checkpoint.

```csharp
using SharedKernel.Vault.Pipeline;

var pipeline = new VaultPipeline(reader, writer, new VaultPasswordPrompts(readPassword, readNewPassword), TimeProvider.System);
PipelineResult result = pipeline.Merge(VaultPipeline.Scan(pipeline.Load(inputPaths, ct), ct), new MergeRequest(), ct);
```

- `VaultPipeline` reads inputs through an `IVaultExportReader`, writes and encrypts the merged vault through an
  `IVaultExportWriter`, and asks for passwords only through `VaultPasswordPrompts`; it never reads passwords from
  arguments or the environment. `PandaBear.SharedKernel.Vault.Bitwarden` and `PandaBear.SharedKernel.Vault.Keeper`
  provide the readers and the writer.
- `Load`, `Scan`, and `Merge` are separate stages, so a host can merge again after each change of decisions
  without reading the inputs again. `Run` runs them all.
- Reports, warnings, and exception messages name files, items, and fields, never vault values. Output files are
  written through a temporary file, readable only by the current user on Unix, and moved into place.
- `DecisionsFile`, `BriefFile`, and `CatalogFile` read and write the JSON files that hold the user's decisions.
- `CatalogFile.CheckSource` verifies the file name and digest recorded by a catalog before applying edits.
  `CatalogApplyResult.CanWrite` checks whether the caller authorized any removals identified by the edit.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Vault`. C# namespace: `SharedKernel.Vault`. Assembly/project identity: `SharedKernel.Vault`.

The project is not packable yet: shipping the vault manager and its engine is a separate decision. Source is public;
registry publication remains deferred.
