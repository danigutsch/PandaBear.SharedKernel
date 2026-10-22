# Vault manager tool: design

## Purpose and scope

`tools/SharedKernel.VaultManager.Tool` is an offline .NET tool, with the command `sharedkernel-vault`, that reads
password vault exports, finds secrets stored in the wrong place, deduplicates and merges items, and writes a
Bitwarden export for import into Bitwarden or a self-hosted Vaultwarden. The first sources are Keeper and
Bitwarden. Other providers and vault operations can be added later.

The motivating case is a partial migration from Keeper to Bitwarden Free. Bitwarden's Keeper importer drops data:
attachments, extra TOTP seeds, passkeys, shared-folder nesting, and every folder after the first. Both vaults
then kept changing, with edits and additions on each side. The result must be one complete vault, and no value
may be lost without the user seeing it.

The tool never contacts a vault service. The user imports the output.

## Package and project identity

- Package ID `PandaBear.SharedKernel.VaultManager.Tool`, namespace `PandaBear.VaultManager.Tool`, project and
  assembly `SharedKernel.VaultManager.Tool`, command `sharedkernel-vault`.
- One project holds the whole tool. Every type is internal; tests use `InternalsVisibleTo`. If another consumer
  needs this logic later, extract a library then.
- Tests live in `tests/SharedKernel.VaultManager.Tool.Tests` and use the repository assertion API.
- Dependencies:
  - System.CommandLine for parsing; it has no transitive packages on .NET 10.
  - Konscious.Security.Cryptography.Argon2 (with Konscious Blake2) to read Argon2id-protected Bitwarden exports.
  - Terminal.Gui for the review UI. It brings 26 transitive packages, including a native `libonigwrap`.
  - Existing repository modules where they fit: `SharedKernel.BuildingBlocks` for IBAN and BIC validation.
- Packaging is framework-dependent. RID-specific and NativeAOT tool packages wait until CI can validate each
  claimed platform. See [command-line tool guidance](../command-line-tools.md).
- On 2026-10-22, a NativeAOT spike published Terminal.Gui 2.5.0 and System.CommandLine 2.0.13 for linux-x64,
  with warnings treated as errors, and the build reported no trim or AOT warnings. The resulting binary
  rendered a two-pane window. The build linked with `CppCompilerAndLinker=gcc` because clang was not installed.
  `libonigwrap.so` ships beside the executable. No other platform has been validated.

## Upstream facts the design depends on

These were checked against upstream sources on 2026-10-22.

- **Vaultwarden import behavior**
  - Import always creates new items and never updates existing ones.
  - Original timestamps are replaced with the import time.
  - One invalid item rejects the whole import.
  - The request limit is 20 MB.
  - Clients keep five password history entries.
  - Item types 6 to 8 need an experimental flag, so the tool writes types 1 to 5 only.
- **Bitwarden field limits** apply to encrypted length. The plaintext budgets are about:
  - 687 UTF-8 bytes for names, field names, and TOTP values
  - 3695 bytes for field values, passwords, and SSH keys
  - 7439 bytes for notes

  Vaultwarden checks only notes. The writer reports values over these budgets and never truncates them.
- **What the import rules imply:** the merged output is the complete vault, and it must be imported into an
  empty vault. Use a new account, or purge the old vault after exporting a backup.
- **Keeper exports**
  - Web vault JSON has no `last_modified` and has sequential `uid` values.
  - Commander JSON has `last_modified` in seconds and real record UIDs.
  - Only Commander `export --format json --zip` includes attachments.
- **Bitwarden password-protected export format**
  - The salt is the UTF-8 bytes of the base64 `salt` string; it is not decoded.
  - The key derivation function is PBKDF2-SHA256 or Argon2id v1.3. For Argon2id, the salt is SHA-256 of the
    salt bytes and memory is the MiB value times 1024 KiB.
  - Keys are stretched with HKDF-Expand only, using info `enc` and `mac`.
  - Values are `2.iv|ct|mac` strings: AES-256-CBC with HMAC-SHA256.
  - The KDF limits are checked before deriving anything: PBKDF2 5,000 to 2,000,000 iterations; Argon2id 2 to 10
    iterations, 16 to 1024 MiB, and parallelism 1 to 16.
- **Account-restricted Bitwarden exports** cannot be decrypted without the account's key. The tool rejects them
  and explains why.

Sources: bitwarden/clients `libs/importer` and `libs/tools/export-vault-core`, bitwarden/sdk-internal
`bitwarden-crypto`, Keeper Commander `keepercommander/importer`, and Vaultwarden `src/api/core/ciphers.rs`.
bitwarden/clients is GPL-3.0, so only interoperability facts are used, never code.

## Pipeline

Read, map, scan, apply accepted placements, match, merge, analyze, write.

1. **Read.** Inputs are:
   - Keeper JSON (web vault and Commander, including Commander `--zip`)
   - Keeper CSV (web vault and Commander)
   - Bitwarden JSON and CSV
   - Bitwarden zip
   - Bitwarden password-protected JSON

   The reader detects the format by content rather than file extension. Readers return items and warnings.
2. **Map.** Every source maps to one item model shaped like a Bitwarden export item, plus provenance (provider,
   file, original ID, modification time). Every source value lands in a typed slot or a custom field. Hidden
   fields hold secrets.
3. **Scan and place.** Secret detection runs on each source item before matching. That way a TOTP seed found in
   Keeper notes is already the login's TOTP when it is compared with Bitwarden.
4. **Match.** The matcher groups items by normalized host plus username, then by normalized title plus type, and
   suggests weaker matches without applying them.
5. **Merge.** The merger works one field at a time (rules below).
6. **Analyze.** The tool reports reused, short, or trivially guessable passwords.
7. **Write.** By default the output is a Bitwarden password-protected export (PBKDF2-SHA256, 600,000 iterations).
   A plaintext export requires an explicit flag. The output file is created with mode 0600 and is never
   overwritten without `--force`.

## Merge rules

A newer item wins only the fields where both sides hold different non-empty values. A value present on one side
always survives. Example: a newer Bitwarden entry has an updated password and no TOTP, and an older Keeper entry
has a TOTP. The result has the new password, the Keeper TOTP, and the old password in the password history.

1. Values that are equal after normalization are kept once.
2. A value present on only one side is taken. Null, empty, and whitespace count as absent. This case is never a
   conflict and never depends on timestamps. Favorite and reprompt take the stricter value.
3. URIs, custom fields, passkeys, and password history are unioned and deduplicated by normalized value.
4. When both sides have different values, the policy picks the winner. The default policy is newest-wins, using
   the latest modification time. The losing value is kept: a password goes to the history with the loser's
   timestamp, and other values become hidden fields named `"<field> (from <source>)"`. Losing notes that do not
   fit a field are appended to the winner's notes. If a side has no timestamp, or the policy is `ask`, the field
   stays unresolved until the user decides.
5. An explicit decision overrides rules 1 to 4, including choosing "empty". Exports carry no deletion markers, so
   this is how a user drops a value that was intentionally removed on one side.
6. The newest member's folder, name, and type are used. The other members' values are recorded.

The decisions file stores group keys, field paths, source identities, and choices. It never stores values.

## Secret detection and placement

Detected secrets become proposals. High-confidence proposals are pre-accepted: a prefix plus a checksum, or a
strong validator. Heuristic proposals need the user to accept them. Source text is removed from notes only
after a proposal is accepted. A secret with no native Bitwarden slot goes into a hidden field on the same item.

- **Scope**
  - notes and custom fields
  - text attachments
  - misfiled core fields, such as a TOTP seed in a password field or a PIN in a card password
  - weak or reused passwords, reported only
- **Engine**
  - A keyword prefilter, then source-generated regular expressions on bounded windows with a 50 ms timeout.
    A timeout becomes a "needs review" finding.
  - Validators: Luhn and card brand ranges, IBAN through `PandaBear.BuildingBlocks.Iban`, base32 and
    `otpauth://` for TOTP, the BIP39 checksum, JWT headers, PEM and OpenSSH structure, and token checksums.
  - Recovery-code detection looks for blocks of same-shaped tokens near keywords or in known file names.
- **Placement examples**
  - A TOTP seed goes to `login.totp`, normalized to `otpauth://`. Later seeds go to hidden fields named
    `TOTP (n)`.
  - Recovery codes go to a hidden field named `Recovery codes`.
  - Security questions become hidden fields named after the question.
  - SSH keys become SSH key items, converted with the BCL and fingerprinted as `SHA256:`.
  - Database URIs fill the login fields.
  - A Bitwarden account recovery code is flagged and never moved into the vault it protects.
- **Licensing of rules.** Rules are curated from gitleaks and Betterleaks (MIT, attributed) and from vendor
  documentation. TruffleHog (AGPL) is not consulted.
- **Findings never contain values.** A finding stores the rule, kind, confidence, field path, offset, and length.

## Attachments

The tool reads attachments from Keeper Commander zips and Bitwarden zips with these bounds:

- at most 1000 entries
- at most 100 MiB expanded
- a compression ratio of at most 100:1, measured on actual bytes
- nesting depth of at most 2
- entries with path traversal are rejected

Each file is classified by magic bytes first. Only text is scanned. Bitwarden JSON imports cannot carry files,
so every attachment stays on a manual upload list in the report.

## Commands

- `sharedkernel-vault inspect <files...>` shows formats, counts, timestamps, attachments, and lossy conversions.
- `sharedkernel-vault scan <files...>` reports secret findings and password health.
- `sharedkernel-vault merge <files...> --output <file>` merges without the UI. Unresolved conflicts update the
  decisions file and exit with code 2 without writing a vault.
- `sharedkernel-vault review <files...>` opens the review UI: groups, a side-by-side field diff, findings, and
  decisions.

Passwords are read only from the terminal without echo, never from arguments or environment variables. Commands
that need a password fail when standard input is not a terminal. Reports and errors go to standard error;
machine-readable output goes to standard output.

## Verification

- Every behavior gets synthetic fixtures and tests. Real exports, tokens, keys, and recovery codes never enter
  the repository. Checksum-valid tokens and private keys are assembled at test runtime, so the public repository
  does not trip secret scanners.
- An independent Node implementation generated the password-protected fixtures, which cross-checks the crypto.
- Interoperability checks are manual and use dummy data: export from a throwaway Vaultwarden account, then
  import the tool's output into a throwaway account.
