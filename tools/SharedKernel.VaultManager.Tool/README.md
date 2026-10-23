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
tool merges them one field at a time. A value present on only one side is always kept, the folder included. When
both sides changed a value, the newer one wins, and the older value is kept in password history or in a hidden field.
The item name and type come from the newest item, or from the first input when no item has a modification time; each
other name is kept in a text field named
`Name (from <input file name>)`. Values that are equal after normalization count as one value, and only the newest
item's form is kept: a user name or website address that differs only in letter case or a trailing `/`, a website
address with another match setting, and a TOTP key with another issuer or label.

## Prerequisites and installation

The tool is a framework-dependent .NET tool and needs the .NET 10 runtime. Install a validated candidate package
from a local or private feed:

```sh
dotnet tool install --global PandaBear.SharedKernel.VaultManager.Tool --prerelease --add-source <feed>
```

The command is `sharedkernel-vault`. Run it in a terminal: passwords are typed at a prompt.

## Usage

Supported inputs are Keeper JSON (web vault and Commander, including Commander `--zip`), Keeper CSV, and Bitwarden
JSON, password-protected JSON, CSV, and zip. The format is detected from the content, not the file extension. A
Bitwarden JSON export with a null item, folder, or collection, a null entry in a list such as the custom fields, or
two folders or collections with one ID is malformed: the command names the first malformed entry by its position
and exits with code 3.

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

4. Decide each pending entry and run the same merge again. Repeat until it exits with code 0. Replace the pending
   entries in `decisions.json` as described below, or decide in the full-screen review UI and save the decisions to
   the same file with `Ctrl+S`:

   ```sh
   sharedkernel-vault review keeper.zip bitwarden.json --decisions decisions.json
   ```

5. Optionally, organize the merged vault with a person or an agent who must not see its secrets: write a catalog,
   decide which notes it may show, let them edit only the catalog, and apply it:

   ```sh
   sharedkernel-vault catalog merged.json --output catalog.json --decisions notes.json
   sharedkernel-vault apply merged.json --catalog catalog.json --output organized.json --decisions notes.json
   ```

6. Import the merged or organized vault into an empty Bitwarden or Vaultwarden vault with its file password, then
   upload the attachments the report lists. Delete the exports, the vaults, the catalog, and the report afterwards.

## Commands

`inspect`, `scan`, `merge`, and `review` take one or more input files, and `catalog` and `apply` take one. Every
command accepts `--help`. The root command also accepts `--version`. An
argument that starts with `@` is a file name like any other: the tool reads no response files, so an export is never
split into arguments. An empty or blank file name is an invalid command line.

### `inspect <inputs>...`

Shows, for each input, the detected format, item counts by type, folders and how many of them hold no item, items
with and without modification times, attachments included in the export or missing from it, and read warnings. It writes this report to
standard output and shows no vault values. Items without a modification time cannot win a conflict under the
`newest` policy.

### `scan <inputs>... [--report <file>]`

Finds secrets stored outside their fields, such as TOTP seeds, recovery codes, or keys in notes, and weak or
reused passwords. A password shorter than 12 characters, of one character class, or equal to the item's user name or
name is reported once per item as weak, without the check that failed. It writes a summary to standard output.

| Option | Description |
| --- | --- |
| `--report <file>` | Writes the full Markdown report: findings with their origin keys and finding IDs, password health, duplicate groups, attachments to upload, and warnings. An existing vault report is replaced; any other existing file and an input never are. |

### `merge <inputs>... --output <file> [options]`

Merges the inputs into one Bitwarden export and writes a summary to standard output.

| Option | Description |
| --- | --- |
| `-o`, `--output <file>` | Required. File to write the merged vault to. It is never an input, and its directory must exist. |
| `--decisions <file>` | Decisions file to read. When fields or findings need a decision, it is created or updated with pending entries. Without this option, a merge that needs decisions asks for it. |
| `--policy <newest\|ask\|prefer:<input file name>>` | How conflicting values are resolved. `newest`, the default, takes the value modified most recently; a missing or tied modification time leaves the field to a decision. `ask` leaves every conflict to a decision. `prefer:<input file name>`, such as `prefer:keeper.zip`, takes the value of that input and falls back to `newest` when that input has no value. The folder follows the policy; the item name and type do not. |
| `--plaintext` | Writes unencrypted Bitwarden JSON instead of a password-protected export. Delete the file after the import. |
| `--report <file>` | Writes the full Markdown report, including the decisions still needed. An existing vault report is replaced; any other existing file and an input never are. |
| `--force` | Allows replacing an existing output file. |

By default the output is a Bitwarden password-protected export. Its password is asked twice at the terminal and
must have at least 12 characters. The merged vault is not written while a field or a proposed finding needs a
decision, or while it holds a value that Bitwarden would reject.

These merges take values from the newer input or from the Keeper export:

```sh
sharedkernel-vault merge keeper.zip bitwarden.json --output merged.json --decisions decisions.json
sharedkernel-vault merge keeper.zip bitwarden.json -o merged.json --decisions decisions.json --policy prefer:keeper.zip
```

### `review <inputs>... [options]`

Opens a full-screen terminal UI for the decisions that `merge` leaves to the decisions file. It merges with the
`newest` policy and merges again after each change, without writing anything until you save or write.

| Option | Description |
| --- | --- |
| `--decisions <file>` | Decisions file to start from and to save to. Without it, the review cannot save decisions. |
| `-o`, `--output <file>` | File to write the merged vault to. It is never an input, and its directory must exist. Without it, the review cannot write the vault. |
| `--plaintext` | Writes unencrypted Bitwarden JSON instead of a password-protected export. Needs `--output`. |
| `--force` | Allows replacing an existing output file. Needs `--output`. |

```sh
sharedkernel-vault review keeper.zip bitwarden.json --decisions decisions.json --output merged.json
```

### `catalog <input> --output <file> [options]`

Writes the catalog of one vault file: a JSON file that a person or an agent may read and edit without seeing any
secret value. The input is any supported export, or a vault this tool wrote, such as `merged.json`. A
password-protected input asks for its password at the terminal.

| Option | Description |
| --- | --- |
| `-o`, `--output <file>` | Required. Catalog file to write. It is never the input, and an existing file is replaced only with `--force`. |
| `--decisions <file>` | Decisions file with the notes decisions. Each item whose notes have no decision gets a pending entry in its `notes` list. |
| `--force` | Allows replacing an existing catalog file. |

```sh
sharedkernel-vault catalog merged.json --output catalog.json --decisions notes.json
```

Each catalog item has a `key`, its origin key, which may not change, and these parts, which may be edited:

- `name`, `folder` (with `/` between levels, or `null`), `additionalFolders`, and `favorite`;
- `notes`: `{ "decision": "Pending" }` until the decisions file decides on them. Only notes whose decision is
  `Share` show their `text`. An item without notes has no `notes`; set `{ "decision": "Share", "text": "..." }` to
  give it some;
- `uris`: website addresses as `{ "value": "...", "match": null }`. An address with user info, a query, or a
  fragment shows as `{ "masked": "https://***@host/path?***", "index": 1 }`: it may be moved, but not changed or
  removed;
- `fields`: text custom fields whose name and value hold no secret finding, as `{ "name": "...", "value": "..." }`.

The top-level `folders` list holds every folder of the vault, including empty ones. `info` lists, for reading only,
the item type, the user name when it holds no secret finding, whether a password or TOTP is present, the number of
passkeys and previous passwords, the names of the custom fields the catalog does not show, and the attachment names
and sizes. The catalog never holds passwords, TOTPs, passkeys, password history, hidden fields, card, identity, or
SSH key values, withheld notes, or any text that holds a secret finding.

The command prints how many notes are shown, withheld, private, or hidden, how many custom fields are withheld, and
how many addresses are masked, and exits with code 0, also while notes wait for a decision.

### `apply <input> --catalog <file> --output <file> [options]`

Applies an edited catalog to the vault file it was made from and writes a Bitwarden export, password-protected
unless `--plaintext` is given.

| Option | Description |
| --- | --- |
| `--catalog <file>` | Required. Edited catalog, made from the input by `catalog`. |
| `-o`, `--output <file>` | Required. File to write the edited vault to. It is never the input or the catalog. |
| `--decisions <file>` | Decisions file with the notes decisions. |
| `--plaintext` | Writes unencrypted Bitwarden JSON instead of a password-protected export. Delete the file after the import. |
| `--force` | Allows replacing an existing output file. |

```sh
sharedkernel-vault apply merged.json --catalog catalog.json --output organized.json --decisions notes.json
```

The catalog applies only to the exact file it was made from: a different file name or any change to the file, which
the catalog records as a SHA-256, exits with code 3 before any password is asked. Secrets, protected fields, masked
addresses, and withheld notes stay as they are. Notes change only when both the catalog and the decisions file
share them; notes whose decision is `Hidden` move into a hidden field named `Notes`. Items cannot be added or
removed. While the catalog changes something it may not, such as the text of withheld notes, a masked address, an
item key, or an empty name, the command writes nothing, prints each problem by item key and name, and exits with code
3. The output creates every folder of the catalog, including empty ones, and the parent of each.

The review needs a terminal. When standard input or standard output is redirected, it exits with code 3 before it
reads any input; run `merge` with `--decisions <file>` and edit the file instead. Before the UI starts, the review
checks the files and inputs as `merge` does and asks for the password of each password-protected input at the
terminal. A problem found then also exits with code 3. Ctrl+C or SIGTERM before the UI starts ends the review at once
with code 1.

Dialogs need a terminal of at least 60 columns by 15 rows. On a smaller terminal, including one that reports a size
of 0x0, the review opens no dialog and shows on its message line what the dialog would have said and how large to
make the terminal. Quitting with unsaved decisions then keeps the review open, so no decision is lost: enlarge the
terminal, or save with `Ctrl+S` and quit again. Writing a password-protected export then writes nothing.

The left side lists the groups of matched items. Each line starts with `!` when a field or finding of the group needs a
decision, `~` when its items hold different values, and `=` when they are equal. The right side shows the selected
group in two tabs: **Fields**, with one column per item, and **Findings**, with the secrets proposed for a better
place. The status bar shows the main keys, and `F1` lists them all:

| Key | Where | Action |
| --- | --- | --- |
| `F1` | anywhere | List every key. |
| `F2` | anywhere | Go to the groups list. |
| `F3` | anywhere | Go to the fields of the selected group. |
| `F4` | anywhere | Go to the findings of the selected group. |
| `F7` | anywhere | Show the next group filter: all, pending, conflict, suggested, identical, unique, has findings. |
| `Ctrl+F` | anywhere | Search the groups by item name. |
| `Ctrl+S` | anywhere | Save the decisions file. |
| `Ctrl+W` | anywhere | Write the merged vault. |
| `F8` | anywhere | Show the notes of every item whose notes hold no finding in catalogs. Asks first. |
| `Ctrl+Q`, `Esc` | anywhere | Quit. With unsaved decisions, the review asks whether to save them first. |
| `Enter` | groups | Go to the fields of the selected group. |
| `m` | groups | Mark the selected group for a link. |
| `l` | groups | Link the marked group with the selected group. |
| `Enter` | fields | Take the value of the item whose column is selected. |
| `a` | fields | Take the selected item's value for every field whose items hold different values. |
| `e` | fields | Drop the field from every item. |
| `Delete` | fields | Remove the decision on the field. |
| `v` | fields | Reveal or mask the values of the field. |
| `s` | fields | Split the selected item out of its group. |
| `n` | fields | Show the selected item's notes in catalogs. |
| `p` | fields | Keep the selected item's notes out of catalogs. |
| `h` | fields | Move the selected item's notes into a hidden field when a catalog is applied. |
| `a` | findings | Accept the finding. |
| `r` | findings | Reject the finding. |
| `Delete` | findings | Remove the decision on the finding. |
| `v` | findings | Reveal or mask the value of the finding. |

Tab, Shift+Tab, and the arrow keys move between and inside the lists and tables.

These values show as `********` until you reveal them: passwords, TOTP keys, password history, hidden custom fields,
card numbers and security codes, SSH private keys, the values of findings, and every field that holds a finding, such
as notes, a user name, or a custom field. Identity social security, passport, and license numbers are masked too,
and so are the user info, query, and fragment of a website address, such as a password or token in it. Revealed
values appear only on the screen; messages, the decisions file, and the command output never hold them. The UI shows
each line break inside a name or value as `↵`: a line feed, a carriage return, the separators U+2028 and U+2029, a
form feed, and NEL U+0085. It shows other control characters as described under Output.

- `Ctrl+S` writes the decisions file, readable only by you, with a pending entry for each field and finding that still
  needs a decision, so `merge` can continue from it.
- `Ctrl+W` writes the merged vault only when no field or proposed finding needs a decision and Bitwarden would accept
  every value. For a password-protected export, it asks twice for a password of at least 12 characters in masked
  fields. It never writes over an input, and replaces an existing output only with `--force`. Bitwarden JSON imports
  cannot carry files, so the merged vault holds no attachments.
- When the UI closes, the review prints on standard output what it saved and wrote, how many fields and findings still
  need a decision, and how many attachments to upload after the import. The count includes the files of Bitwarden zip
  attachment folders that match no item or several items. When it wrote the merged vault, it lists each of these files
  and where to find it, as the report of `merge` does, with the number of items that the folder of an unassigned file
  matches; upload them after the import. On standard error, it prints the writer's
  warnings, each accepted finding that it could not move, and a note when decisions changed since the last save were
  not saved.
- After you quit with `Ctrl+Q` or `Esc`, the review exits with code 0. Inside the UI, `Ctrl+C` is a key that does not
  end the review. SIGTERM or SIGINT closes any open dialog, ends the review without saving, restores the terminal, and
  exits with code 1. A signal that arrives while the review merges or writes takes effect when that work ends. When
  that takes longer than two seconds, the process ends with code 130 or 143 and leaves the terminal in the UI's mode,
  which `reset` restores.

## Decisions file

The decisions file is JSON. A merge with `--decisions` reads it. When something needs a decision, the merge
rewrites the file: it rebuilds the pending entries, one for each field and finding without a decision, and keeps
all other entries as they are. Pending entries count as no decision. A merge that needs no decision leaves the
file unchanged. Item names are written as typed, apostrophes and accents included. Control and format characters,
such as a right-to-left override, are escaped, so a name cannot reorder or hide text in an editor. This is a file
after a merge that needs two decisions:

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
  pre-accepted finding, add an entry with its finding ID from the report to the `findings` list.
- **Notes decisions.** The optional `notes` list decides, per item, how `catalog` and `apply` handle its notes:
  `{ "originKey": "bitwarden|merged.json|3f2…", "decision": "Share", "itemName": "GitHub", "findings": 0 }`.
  `Share` shows the notes in catalogs and lets `apply` take the edited text, `Private` keeps them out of catalogs,
  and `Hidden` keeps them out and moves them into a hidden field named `Notes` when a catalog is applied. `catalog`
  rebuilds the `Pending` entries, one for each item whose notes have no decision, with the number of findings in the
  notes; a pending entry withholds the notes. `merge` ignores this list. In `review` of the same file, `n`, `p`, `h`, and
  `F8` set these decisions.
- **Links and splits.** `links` and `splits` take pairs of origin keys, such as
  `{ "first": "keeper|keeper.json|rec-1", "second": "bitwarden|bitwarden.json|bw-1" }`, that must or must never
  end up in one group. A split wins over a link.

The merge warns on standard error about each decision whose group, field, origin key, or finding ID matches
nothing, so a typo does not leave the policy or a pre-accepted finding in effect unnoticed. A `Member` choice that
names an item outside its group is no decision: the policy decides the field, and when the field needs a decision,
its pending entry takes the choice's place. The merge reads only the properties shown above, each at most once in
its object. Any other property, such as a misspelled `findings`, and a repeated one, such as a second `findings`
list, stop the merge with exit code 3 before any password is asked, and the error names the file and the
property's JSON path.

## Exit codes

| Code | Meaning |
| --- | --- |
| 0 | Success. |
| 1 | Invalid command line, with the error on standard error and help on standard output, both shown as described under Output; canceled; or an unexpected error. Ctrl+C or SIGTERM cancels a command; inside the review UI, `Ctrl+C` is a key, and SIGTERM or SIGINT cancels, as described under `review`. Canceled at a password prompt or before the review UI starts, the command ends at once. Otherwise `inspect`, `scan`, and `merge` wait up to 1.5 seconds for their work to stop at its next check: no file write starts after the cancel, and a write already under way either completes or leaves its file unchanged, as described under Security. Work still running after 1.5 seconds ends with the process. An unexpected error shows the exception type and stack trace, but not its message, which can contain vault values. |
| 2 | `merge` only: fields or findings need a decision. No vault is written; with `--decisions`, the decisions file holds a pending entry for each. |
| 3 | An input or output problem: an input that is unreadable, unsupported, malformed, or account-restricted, or whose password is wrong; a decisions file that is not valid, such as one with an unknown or repeated property; a catalog that is not valid, was made from another file or another version of the input, or changes what it may not; an output that exists without `--force` or is an input; a missing output directory; a report path that names an existing file that is not a vault report; a report or decisions file that cannot be written; values that Bitwarden would reject; a password needed while standard input is not a terminal, or no acceptable new password in three attempts; or `review` started while standard input or output is not a terminal. No vault is written, except when `merge` writes the vault and then cannot write the report: standard output then names the written vault, and the error says that the vault is written and the report is not. When `merge` also needs decisions, it still adds their pending entries. |
| 130, 143 | Ctrl+C or SIGTERM during start-up, before the tool handles it, or a canceled command that did not end within two seconds. |

## Output

`inspect` writes its report to standard output, and `scan` and `merge` write a summary there. `merge` names the
written vault there before it writes the report, and `review` writes its summary there when the UI closes.
Warnings, errors, and password prompts go to standard error. Messages name inputs, items, fields, origin keys, and
finding IDs, never values. In the names, paths, keys, prompts, and messages that the commands print and the reports
hold, carriage returns, line feeds, tabs, and the line and paragraph separators U+2028 and U+2029 show as spaces, and
every other control character and every bidirectional embedding, override, and isolate shows as U+FFFD, so an export
can neither send escape sequences to the terminal nor reorder the text around a name. The error, typo corrections,
and help of an invalid command line keep their line breaks and show other characters the same way, so an argument
that System.CommandLine repeats cannot send escape sequences either.

## Limits

- An input file may hold at most 256 MiB.
- Zip exports may hold at most 1000 entries and 100 MiB when expanded, with a compression ratio of at most 100:1
  and nesting of at most two levels.
- Bitwarden JSON imports cannot carry files. Attachments stay on the upload list of the report, which also lists the
  files of Bitwarden zip attachment folders that match no item or several items. The summaries of `scan`, `merge`, and
  `review` count them all.
- A Bitwarden individual vault holds one folder per item. An item's other Keeper folders, other Bitwarden
  collections, and the folders of merged items that lost are listed in its `Additional folders` text field. The
  output creates every folder of the inputs, including empty ones, and the parent of each, so Bitwarden nests them.
  A `/` inside a Keeper folder name becomes `-`. Keeper shared folder permissions, members, and teams are not
  carried over.
- Bitwarden saves one passkey per login. A merged login keeps the passkeys of all its items, so the merge warns
  about each login that holds more than one; split its group to keep each passkey on a login of its own.
- Bitwarden and Vaultwarden reject a whole import when one item is invalid, so the merge writes no vault while a
  value exceeds Bitwarden's limits. The report and the errors on standard error name each one.

## Security

- The tool makes no network calls. You import its output yourself.
- Output is a Bitwarden password-protected export by default: PBKDF2-SHA256 with 600,000 iterations,
  AES-256-CBC, and HMAC-SHA256. A plaintext export needs `--plaintext`.
- On Unix, the output, the report, and the decisions file are created readable only by you. Each is written to a
  temporary file, `.<file name>.<random>.tmp`, in the same directory and then moved into place, so a file is either
  written completely or left unchanged. A write that fails or is canceled deletes its temporary file. If the process
  is killed during a write, or a canceled command ends while a write is still running, that file can remain and hold
  part of the content, unencrypted with `--plaintext`. Delete it.
- An existing output is replaced only with `--force`, and an input is never replaced. Paths are compared after
  resolving symbolic links and ignoring case, so a path that differs from an input only in case is refused too.
- Passwords are read from the terminal without echo. They are never accepted as arguments or environment
  variables. Wait for the prompt before you type a password: the terminal shows what is typed earlier. When
  standard input is not a terminal, a command that needs a password exits with code 3 before it reads any vault,
  and names the input or output that needs the password.
- A catalog holds item names, folder names, user names, website addresses with their user info, query, and fragment
  masked, text custom fields and notes without secret findings, and attachment names, and never secret values. The
  secret scanner can miss a secret, so notes appear only after you share them in the decisions file. An agent that
  can write the decisions file can share notes itself; keep that file out of its reach when it must not.
- Reports, warnings, errors, and the decisions file never contain secret values. They do contain item names,
  folder names, file names, and origin keys. An origin key holds the record or item ID of an export, or, for an item
  without one, such as a CSV row, a hash of its type, title, and first host.
- .NET strings cannot be wiped from memory, so vault values remain in process memory until the runtime reclaims
  it. Work on an encrypted disk, keep exports out of synced folders, disable core dumps, and delete exports
  after a successful import.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.VaultManager.Tool`. C# namespace: `PandaBear.VaultManager.Tool`.
Assembly and project identity: `SharedKernel.VaultManager.Tool`. Command: `sharedkernel-vault`.

The source is public. Registry publication remains deferred. Consume a validated candidate from an explicitly
configured local or private feed. This README does not imply the package is available on nuget.org.

The design and its upstream evidence are in [the vault manager plan](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/docs/plans/vault-manager.md).
