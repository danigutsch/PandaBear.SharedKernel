# PandaBear.SharedKernel.Vault.Keeper

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Keeper provider for `PandaBear.SharedKernel.Vault`. It reads Keeper exports offline into provider-neutral vault
items: Keeper JSON from the web vault or Commander, the Commander zip with attachments, and Keeper CSV, as described in
Keeper's [vault export](https://docs.keeper.io/user-guides/export-and-reports/vault-export) and
[JSON import](https://docs.keeper.io/user-guides/import-records-1/import-json) documentation.

## Usage

```csharp
using SharedKernel.Vault.Keeper;

VaultReadResult json = KeeperJsonReader.Read(text, "keeper.json");
VaultReadResult zip = KeeperJsonReader.ReadZip("keeper.zip");
VaultReadResult csv = KeeperCsvReader.Read(text, "keeper.csv");
```

- Each reader returns the items and warnings that name items and fields, never values. Zip archives are opened within
  the size limits of `SafeArchive` in `PandaBear.SharedKernel.Vault`.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Vault.Keeper`. C# namespace: `SharedKernel.Vault.Keeper`. Assembly/project identity: `SharedKernel.Vault.Keeper`.

The project is not packable yet: shipping the vault manager and its engine is a separate decision. Source is public;
registry publication remains deferred.
