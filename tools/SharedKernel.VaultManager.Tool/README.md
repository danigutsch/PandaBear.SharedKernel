# PandaBear.SharedKernel.VaultManager.Tool

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

An offline tool that reads password vault exports, proposes better places for secrets kept in notes, and merges
duplicate items field by field. It writes a Bitwarden export that Bitwarden or a self-hosted Vaultwarden can
import. The supported sources are Keeper and Bitwarden.

**Status:** under development. Command behavior is not complete. Do not use it on real vault data yet.

For the whole migration, from exporting both vaults to importing the result, see the
[vault migration guide](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/docs/vault-migration.md).

## Why it exists

A vault migrated in pieces can end up split across two services. Bitwarden's Keeper importer drops attachments,
extra TOTP seeds, passkeys, and folder nesting. If both vaults then keep changing, neither one is complete. This
tool merges them one field at a time. A value present on only one side is always kept. When both sides changed a
value, the newer one wins, and the older value is kept in password history or in a hidden field.

## Prerequisites and installation

The tool is a framework-dependent .NET tool and needs the .NET 10 runtime. Install a validated candidate package
from a local or private feed:

```sh
dotnet tool install --global PandaBear.SharedKernel.VaultManager.Tool --prerelease --add-source <feed>
```

The command is `sharedkernel-vault`. Run it in a terminal: passwords are typed at a prompt.

## Usage

Supported inputs are Keeper JSON (web vault and Commander, including Commander `--zip`), Keeper CSV, and Bitwarden
JSON, password-protected JSON, CSV, and zip. The format is detected from the content, not the file extension.

1. Check what each export holds:

   ```sh
   sharedkernel-vault inspect keeper.zip bitwarden.json
   ```

2. Review the secrets found outside their fields and the weak or reused passwords:

   ```sh
   sharedkernel-vault scan keeper.zip bitwarden.json --report scan.md
   ```

3. Merge. When fields or findings need a decision, the merge writes no vault, adds pending entries to the
   decisions file, and exits with code 2:

   ```sh
   sharedkernel-vault merge keeper.zip bitwarden.json --output merged.json --decisions decisions.json --report merge.md
   ```

4. Replace each pending entry in `decisions.json` with a decision, as described below, and run the same merge
   again. Repeat until it exits with code 0.

5. Import `merged.json` into an empty Bitwarden or Vaultwarden vault with its file password, then upload the
   attachments the report lists. Delete the exports, the merged vault, and the report afterwards.

## Commands

Every command takes one or more input files and accepts `--help`. The root command also accepts `--version`.

### `inspect <inputs>...`

Shows, for each input, the detected format, item counts by type, folders, items with and without modification
times, attachments included in the export or missing from it, and read warnings. It writes this report to
standard output and shows no vault values. Items without a modification time cannot win a conflict under the
`newest` policy.

### `scan <inputs>... [--report <file>]`

Finds secrets stored outside their fields, such as TOTP seeds, recovery codes, or keys in notes, and weak or
reused passwords. It writes a summary to standard output.

| Option | Description |
| --- | --- |
| `--report <file>` | Writes the full Markdown report: findings with their origin keys and finding IDs, password health, duplicate groups, attachments to upload, and warnings. An existing report is replaced; an input never is. |

### `merge <inputs>... --output <file> [options]`

Merges the inputs into one Bitwarden export and writes a summary to standard output.

| Option | Description |
| --- | --- |
| `-o`, `--output <file>` | Required. File to write the merged vault to. It is never an input, and its directory must exist. |
| `--decisions <file>` | Decisions file to read. When fields or findings need a decision, it is created or updated with pending entries. Without this option, a merge that needs decisions asks for it. |
| `--policy <newest\|ask\|prefer:<input file name>>` | How conflicting values are resolved. `newest`, the default, takes the value modified most recently; a missing or tied modification time leaves the field to a decision. `ask` leaves every conflict to a decision. `prefer:<input file name>`, such as `prefer:keeper.zip`, takes the value of that input and falls back to `newest` when that input has no value. |
| `--plaintext` | Writes unencrypted Bitwarden JSON instead of a password-protected export. Delete the file after the import. |
| `--report <file>` | Writes the full Markdown report, including the decisions still needed. An existing report is replaced; an input never is. |
| `--force` | Allows replacing an existing output file. |

By default the output is a Bitwarden password-protected export. Its password is asked twice at the terminal and
must have at least 12 characters. Nothing is written while a field or a proposed finding needs a decision, or
while the merged vault holds a value that Bitwarden would reject.

These merges take values from the newer input or from the Keeper export:

```sh
sharedkernel-vault merge keeper.zip bitwarden.json --output merged.json --decisions decisions.json
sharedkernel-vault merge keeper.zip bitwarden.json -o merged.json --decisions decisions.json --policy prefer:keeper.zip
```

## Decisions file

The decisions file is JSON. A merge with `--decisions` reads it. When something needs a decision, the merge
rewrites the file: it rebuilds the pending entries, one for each field and finding without a decision, and keeps
all other entries as they are. Pending entries count as no decision. A merge that needs no decision leaves the
file unchanged. This is a file after a merge that needs two decisions:

```json
{
  "version": 2,
  "groups": [
    {
      "groupKey": "4943740a016f2549",
      "choices": [
        {
          "fieldPath": "login.password",
          "kind": "Pending",
          "options": [
            { "originKey": "keeper|keeper.json|rec-1", "itemName": "Example" },
            { "originKey": "bitwarden|bitwarden.json|bw-1", "itemName": "Example" }
          ]
        }
      ]
    }
  ],
  "links": [],
  "splits": [],
  "findings": [
    {
      "originKey": "keeper|keeper.json|rec-github",
      "choices": [{ "findingId": "notes-password@notes:10", "decision": "Pending" }],
      "itemName": "GitHub"
    }
  ]
}
```

- **Field decisions.** A group is a set of duplicate items, named by its group key. To decide a field, replace
  `"kind": "Pending"` with `"kind": "Member"` and add `"originKey"` with one of the options to take that item's
  value, or with `"kind": "Empty"` to drop every value of the field. `Member` applies to `name`, `folder`, and
  single-value fields such as `notes`, `login.username`, `login.password`, `login.totp`, `card.number`,
  `identity.email`, or `sshKey.privateKey`. `Empty` applies to single-value fields and to `fields`,
  `passwordHistory`, `login.uris`, and `login.fido2Credentials`.
- **Finding decisions.** A finding is a secret proposed for a better place, named by its finding ID. Pre-accepted
  findings, which have a checksum or a strong validator, are applied unless a decision rejects them. Other findings
  are applied only when accepted. Replace `"decision": "Pending"` with `"Accepted"` or `"Rejected"`. To reject a
  pre-accepted finding, add an entry with its finding ID from the report.
- **Links and splits.** `links` and `splits` take pairs of origin keys, such as
  `{ "first": "keeper|keeper.json|rec-1", "second": "bitwarden|bitwarden.json|bw-1" }`, that must or must never
  end up in one group. A split wins over a link.

The merge warns on standard error about each decision whose group, field, origin key, or finding ID matches
nothing, so a typo does not leave the policy or a pre-accepted finding in effect unnoticed.

## Exit codes

| Code | Meaning |
| --- | --- |
| 0 | Success. |
| 1 | Invalid command line, with the error on standard error and help on standard output; canceled with Ctrl+C or SIGTERM; or an unexpected error. An unexpected error shows the exception type and stack trace, but not its message, which can contain vault values. |
| 2 | `merge` only: fields or findings need a decision. No vault is written; with `--decisions`, the decisions file holds a pending entry for each. |
| 3 | An input or output problem: an input that is unreadable, unsupported, or account-restricted, or whose password is wrong; an output that exists without `--force` or is an input; a missing output directory; values that Bitwarden would reject; or a password needed while standard input is not a terminal. No vault is written. When `merge` also needs decisions, it still adds their pending entries. |

## Output

`inspect` writes its report to standard output, and `scan` and `merge` write a summary there. Warnings, errors,
and password prompts go to standard error. Messages name inputs, items, fields, origin keys, and finding IDs, never
values.

## Limits

- An input file may hold at most 256 MiB.
- Zip exports may hold at most 1000 entries and 100 MiB when expanded, with a compression ratio of at most 100:1
  and nesting of at most two levels.
- Bitwarden JSON imports cannot carry files. Attachments stay on the upload list of the report.
- Bitwarden and Vaultwarden reject a whole import when one item is invalid, so the merge writes nothing while a
  value exceeds Bitwarden's limits. The report and the errors on standard error name each one.

## Security

- The tool makes no network calls. You import its output yourself.
- Output is a Bitwarden password-protected export by default: PBKDF2-SHA256 with 600,000 iterations,
  AES-256-CBC, and HMAC-SHA256. A plaintext export needs `--plaintext`.
- On Unix, the output, the report, and the decisions file are created readable only by you. Each is written to a
  temporary file and moved into place. An existing output is replaced only with `--force`, and an input is never
  replaced.
- Passwords are read from the terminal without echo. They are never accepted as arguments or environment
  variables. When standard input is not a terminal, a command that needs a password exits with code 3 before it
  reads any vault, and names the input or output that needs the password.
- Reports, warnings, errors, and the decisions file never contain secret values. They do contain item names,
  folder names, file names, and origin keys.
- .NET strings cannot be wiped from memory, so vault values remain in process memory until the runtime reclaims
  it. Work on an encrypted disk, keep exports out of synced folders, disable core dumps, and delete exports
  after a successful import.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.VaultManager.Tool`. C# namespace: `PandaBear.VaultManager.Tool`.
Assembly and project identity: `SharedKernel.VaultManager.Tool`. Command: `sharedkernel-vault`.

The source is public. Registry publication remains deferred. Consume a validated candidate from an explicitly
configured local or private feed. This README does not imply the package is available on nuget.org.

The design and its upstream evidence are in [the vault manager plan](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/docs/plans/vault-manager.md).
