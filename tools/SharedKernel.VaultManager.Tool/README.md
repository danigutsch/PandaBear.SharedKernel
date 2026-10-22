# PandaBear.SharedKernel.VaultManager.Tool

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

An offline tool that reads password vault exports, proposes better places for secrets kept in notes, and merges
duplicate items field by field. It writes a Bitwarden export that Bitwarden or a self-hosted Vaultwarden can
import. The first supported sources are Keeper and Bitwarden.

**Status:** under development. Command behavior is not complete. Do not use it on real vault data yet.

## Why it exists

A vault migrated in pieces can end up split across two services. Bitwarden's Keeper importer drops attachments,
extra TOTP seeds, passkeys, and folder nesting. If both vaults then keep changing, neither one is complete. This
tool merges them one field at a time. A value present on only one side is always kept. When both sides changed a
value, the newer one wins, and the older value is kept in password history or in a hidden field.

## Security

- The tool makes no network calls. You import its output yourself.
- Output is a Bitwarden password-protected export by default: PBKDF2-SHA256 with 600,000 iterations,
  AES-256-CBC, and HMAC-SHA256. A plaintext export needs an explicit flag.
- On Unix, output files are created readable only by you, and existing files are replaced only with `--force`.
- Passwords are read from the terminal without echo. They are never accepted as arguments or environment
  variables.
- Reports, warnings, errors, and the decisions file never contain secret values. They do contain item names.
- .NET strings cannot be wiped from memory, so vault values remain in process memory until the runtime reclaims
  it. Work on an encrypted disk, keep exports out of synced folders, disable core dumps, and delete exports
  after a successful import.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.VaultManager.Tool`. C# namespace: `PandaBear.VaultManager.Tool`.
Assembly and project identity: `SharedKernel.VaultManager.Tool`. Command: `sharedkernel-vault`.

The source is public. Registry publication remains deferred. Consume a validated candidate from an explicitly
configured local or private feed. This README does not imply the package is available on nuget.org.

The design and its upstream evidence are in [the vault manager plan](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/docs/plans/vault-manager.md).
