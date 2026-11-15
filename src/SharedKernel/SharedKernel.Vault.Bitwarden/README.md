# PandaBear.SharedKernel.Vault.Bitwarden

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Bitwarden provider for `PandaBear.SharedKernel.Vault`. It reads Bitwarden exports offline into provider-neutral vault
items and writes JSON exports that [Bitwarden](https://bitwarden.com/help/condition-bitwarden-import/) and Vaultwarden
import.

## Usage

```csharp
using PandaBear.Vault.Bitwarden;

VaultExport vault = BitwardenJsonReader.Read(json, "bitwarden.json", warnings);
VaultWriteResult written = BitwardenExportWriter.Instance.Write(vault);
string protectedExport = BitwardenExportWriter.Instance.Encrypt(written.Json, password);
```

- `BitwardenJsonReader`, `BitwardenCsvReader` (individual and organization headers), and `BitwardenZipReader` read
  unencrypted exports. `PasswordProtectedExport.Decrypt` reads a password-protected export, with PBKDF2-SHA256 or
  Argon2id key derivation, and `CheckEnvelope` checks its settings before a password is asked. Account-restricted
  exports cannot be read offline.
- `BitwardenExportWriter` implements `IVaultExportWriter`: it writes an unencrypted individual-vault export, reports
  values over Bitwarden's encrypted field limits as errors, and encrypts the export with a file password,
  PBKDF2-SHA256, and 600,000 iterations.
- Argon2id uses `Konscious.Security.Cryptography.Argon2`. Derived keys and decrypted bytes are cleared after use.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Vault.Bitwarden`. C# namespace: `PandaBear.Vault.Bitwarden`. Assembly/project identity: `SharedKernel.Vault.Bitwarden`.

The project is not packable yet: shipping the vault manager and its engine is a separate decision. Source is public;
registry publication remains deferred.
