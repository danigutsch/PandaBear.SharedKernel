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
  - Terminal.Gui for the review UI. It brings 23 transitive packages, including `Onigwrap` with a native
    `libonigwrap`.
  - Existing repository modules where they fit: `SharedKernel.BuildingBlocks` for IBAN and BIC validation.
- Packaging is framework-dependent. RID-specific and NativeAOT tool packages wait until CI can validate each
  claimed platform. See [command-line tool guidance](../command-line-tools.md).
- NativeAOT for linux-x64 builds Terminal.Gui 2.5.0 and System.CommandLine 2.0.13 without trim or AOT warnings,
  with warnings treated as errors and gcc as the linker (`CppCompilerAndLinker=gcc`). The binary renders a
  two-pane window, and `libonigwrap.so` ships beside it. No other platform is validated.

## Upstream facts the design depends on

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

   The reader detects the format by content rather than file extension. Readers return items and warnings. A
   Bitwarden JSON export with a null item, folder, or collection, a null entry in a list such as the custom fields,
   or two folders or collections with one ID is malformed: the command exits with code 3, and the message names the
   first malformed entry by its position, never by its content. Items in the Bitwarden trash are skipped with a
   warning that counts them. A Keeper Commander zip entry that no record lists, or that is not under `files/`, gets
   a warning that names it.
2. **Map.** Every source maps to one item model shaped like a Bitwarden export item, plus provenance (provider,
   file, original ID, modification time). Every source value lands in a typed slot or a custom field. Hidden
   fields hold secrets. Each item gets its origin key as read, and decisions name the item by that key even after
   placement changes the item. An item without an export ID, such as a CSV row, gets a key from a hash of its type,
   title, and the host of its first address, read from the address masked as the catalog shows it; the user name and
   any credential in the address stay out of the hash, so a decisions file or a brief cannot confirm a guessed one. An SSH key item without a public key or fingerprint is completed from the first
   PEM block of its private key; other text stored with the key, such as a second key, moves to a hidden field,
   where secret detection finds it. If the key cannot be converted, for example because it is encrypted, the item
   becomes a secure note that keeps the key in a hidden field.
3. **Scan and place.** Secret detection runs on each source item, and on text attachments inside Keeper Commander
   and Bitwarden zips, before matching. That way a TOTP seed found in Keeper notes is already the login's TOTP
   when it is compared with Bitwarden. Items that placement creates, such as SSH key items, are named after their
   source item, with a number when one source item creates several, and their origin key is built from the source
   item and finding. They match and merge like any other item, and items created from one source item never match
   each other by title.
4. **Match.** The matcher groups items by normalized host plus username, then by normalized title plus type, and
   suggests weaker matches without applying them.
5. **Merge.** The merger works one field at a time (rules below).
6. **Analyze.** The tool reports reused and weak passwords. A password shorter than 12 characters, of one character
   class, or equal to the item's user name or name is reported once per item as weak, without the check that failed,
   like Bitwarden's weak-password report. The tool groups the items once per password, and each reuse issue names at
   most three other items on other hosts and counts the rest, such as `Mail, Bank, Shop, and 2 more`, so the analysis
   and the password health table grow linearly with the vault. The report holds no password, hash, length, or reason.
7. **Write.** By default the output is a Bitwarden password-protected export (PBKDF2-SHA256, 600,000 iterations).
   A plaintext export requires an explicit flag. The output file is created with mode 0600 and is never
   overwritten without `--force`, and it may not be one of the inputs. The vault is not written while a field needs a
   decision, while a proposed finding has no decision, or while the writer reports a value that Bitwarden would
   reject. The writer warns when its estimate of the import request exceeds Vaultwarden's 20,000,000-byte limit: the
   export as compact JSON, with each value and folder name as an encrypted string and an encrypted SHA-256 checksum
   for each login URI. The tool does not split an export.

## Folders

A Bitwarden individual vault holds one folder per item, and `/` in a folder name separates levels. Bitwarden nests
`A/B` under `A` only when a folder named exactly `A` exists, and its importer creates no missing parent.

- Readers keep every folder an input declares, including folders that hold no item: Bitwarden folders and
  collections, Keeper shared folder paths, and every Keeper record folder.
- An item takes one folder: a Keeper record its first folder, a Bitwarden item its folder or else its first
  collection. Its other folders and collections are listed, one per line, in a text field named
  `Additional folders`, and a conversion note names them.
- A Keeper path joins the shared folder and the folder inside it with `/`. A `/` inside a Keeper folder name becomes
  `-`, as Bitwarden's Keeper importer does, and a conversion note names the original.
- The writer creates every declared folder, every item folder, and the parent of each, so the import shows the same
  tree.
- Not carried over: Keeper shared folder permissions, members, and teams, which a warning counts and the conversion
  notes name per item.

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
6. The folder is a single value under rules 2, 4, and 5: a folder on one side is kept, and different folders go
   to the policy. The losing folder is listed in the `Additional folders` field and noted in the report.
7. The newest member's name and type are used, or the first input's when no member has a modification time, under
   every policy. Each other name is kept in a text field named `"Name (from <source>)"`, and another type is noted
   in the report.
8. Values equal after normalization keep the newest member's form: user names and URIs compare without letter case,
   URIs without their match setting, and TOTP keys without issuer and label.
9. Passkeys are unioned like other lists. Bitwarden saves one passkey per login, so the writer warns about each login
   with more than one; splitting the group keeps each passkey on its own login.

The decisions file stores group keys, field paths, origin keys, item names, and choices, plus finding identifiers
with the user's decision, per item origin key. It never stores values. When a merge needs decisions, it adds a
pending entry for each field and each proposed finding without a decision: a field entry lists the members to
choose from by origin key and item name, and a finding entry names its item. A pending entry counts as no decision,
and so does a member choice that names an item outside its group: the policy decides that field, and when the field
needs a decision, its pending entry takes the choice's place. A pre-accepted finding is applied unless the file
rejects it; a proposed finding is applied only when the file accepts it, and while a proposed finding has no
decision, the merge writes no vault. Item names are written as typed, except that control and format characters,
such as a right-to-left override, are escaped. The file is read strictly: a property that the format does not
define, or a property given twice in one object, stops the merge, and the error names the file and the property's
JSON path. A misspelled section therefore cannot drop the decisions it holds, and a repeated section cannot replace
them.

## Secret detection and placement

Detected secrets become proposals. High-confidence proposals are pre-accepted: a prefix plus a checksum, or a
strong validator. Heuristic proposals need the user to accept them. Source text is removed from notes only
after a proposal is accepted, and only the placed value and the label that names its field: other text on its
lines, and a header line above a block of recovery codes, stays in the notes. A secret with no native Bitwarden slot goes into a hidden field on the same item.

- **Scope**
  - notes and custom fields
  - text attachments
  - misfiled core fields, such as a TOTP seed in a password field or a PIN in a card password
  - weak or reused passwords, reported only
- **Engine**
  - A keyword prefilter, then source-generated regular expressions on bounded windows with a 1 s match timeout.
    A match that times out runs once more; a second timeout turns its window into a "needs review" finding. After
    three timed-out windows of one rule in a text, the rest of that text becomes one "needs review" finding for the
    rule.
  - Validators: Luhn and card brand ranges, IBAN through `PandaBear.BuildingBlocks.Iban`, base32 and
    `otpauth://` for TOTP, the BIP39 checksum, JWT headers, PEM and OpenSSH structure, and token checksums.
  - Recovery-code detection looks for blocks of same-shaped tokens near keywords or in known file names.
- **Placement examples**
  - A TOTP seed goes to `login.totp`, normalized to `otpauth://`. Later seeds go to hidden fields named
    `TOTP (n)`.
  - Recovery codes go to a hidden field named `Recovery codes`.
  - A value on a labeled line, such as `prod api token: <token>`, goes to a hidden field named after the label, also
    when a catalog rule recognizes the value, so two labeled tokens stay apart.
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
so every attachment stays on a manual upload list in the report. A Bitwarden zip names each item's attachment
folder after the item; a folder that matches no item or several items is attached to none. Its files get a second
table in the report (folder, file, source, and the number of matching items), and the `scan`, `merge`, and `review`
summaries count them with the other attachments to upload.

## Commands

- `sharedkernel-vault inspect <inputs...>` shows, per input, the detected format, item counts by type, folders and
  how many hold no item, items with and without modification times, attachments in the export or missing from it, and read warnings. It
  writes this report to standard output and shows no values.
- `sharedkernel-vault scan <inputs...> [--report <file>]` reports secret findings and password health without
  values. It writes a summary to standard output; `--report` writes the full Markdown report.
- `sharedkernel-vault merge <inputs...> --output <file> [--decisions <file>] [--policy <newest|ask|prefer:<input file name>>] [--plaintext] [--report <file>] [--force] [--brief <file>]`
  merges without the UI. The default policy is `newest`. The output is a Bitwarden password-protected export, with
  the password asked twice at the terminal, unless `--plaintext` is given. `--force` allows replacing an existing
  output; an input is never replaced. When fields still need a decision, or proposed findings need accepting or
  rejecting, the merge writes no vault and exits with code 2. With `--decisions`, it first creates or updates the
  decisions file with pending entries for the user to edit; without it, it asks to rerun with
  `--decisions <file>`. Pending entries carry no values: a field entry lists the members to choose from by origin
  key and item name, and a finding entry names its item. They count as no decision, and an update keeps every
  other entry. The merge warns about decisions whose group, field, origin key, or finding identifier matches
  nothing. Each input is parsed and scanned once, and each password asked once; format detection reads each input
  before that. With `--report`, the merge names the written vault on standard output before it writes the report.
  With `--brief`, it writes the brief (see Brief) after the first merge and before the vault, so a brief that cannot be
  written stops the merge before anything else is written.
- `sharedkernel-vault catalog <input> --output <file> [--decisions <file>] [--force]` writes the catalog of one
  vault file and adds pending notes entries to the decisions file (see Catalog).
- `sharedkernel-vault apply <input> --catalog <file> --output <file> [--decisions <file>] [--plaintext] [--force]`
  applies an edited catalog to the file it was made from and writes a Bitwarden export.
- `sharedkernel-vault review <inputs...> [--decisions <file>] [--output <file>] [--plaintext] [--force]` opens the
  full-screen review UI: groups, a side-by-side field diff, findings, and decisions. See [Review UI](#review-ui).

An argument that starts with `@` is a file name: the tool turns off System.CommandLine's response files, which would
split an export into arguments and print its values in parse errors. Empty or blank file names are rejected while
parsing.

Exit codes:

- 0: success.
- 1: invalid command line, cancellation, or an unexpected error (System.CommandLine's code for these). Ctrl+C or
  SIGTERM cancels the command's token, which the pipeline stages and the file writes observe. Canceled at a password
  prompt, or before the review UI starts, a command ends at once. Canceled elsewhere, `inspect`, `scan`, and `merge`
  wait up to 1.5 seconds, less than System.CommandLine's two-second process termination timeout, for the work to stop
  at its next check: no file write starts after the cancel, and a write already under way either completes or deletes
  its temporary file and leaves its file unchanged. Work still running after 1.5 seconds ends with the process, and a
  write cut off that way can leave its temporary file behind. Inside the review UI, Ctrl+C is a key; see
  [Review UI](#review-ui).
- 2: decisions needed: the merge wrote, or would write, pending decisions.
- 3: an input or output problem: an unreadable, unsupported, malformed, or account-restricted input, a wrong password, a
  decisions file that is not valid, an output that exists without `--force` or is an input, a missing output directory,
  a report path that names an existing file that is not a vault report, a brief path that names an existing file that
  is not a brief, a report, brief, or decisions file that cannot be written, writer errors that block writing, a password needed without a
  terminal, no acceptable new password in three attempts, or `review` without a terminal. When the report cannot be
  written after the vault, the error says that the vault is written and the report is not.
- 130 or 143: Ctrl+C or SIGTERM during start-up, before System.CommandLine handles it, or a canceled command that
  did not end within the two-second process termination timeout.

Passwords are read only from a terminal prompt without echo, never from arguments or environment variables.
Password-protected inputs and an encrypted output need a terminal: without one, a command that needs a password
fails with exit code 3 before doing any work and names the input or output that needs it. The `inspect` report and
the `scan`, `merge`, and `review` summaries go to standard output; diagnostics, warnings, errors, and prompts go to
standard error. Every printed or reported name, path, key, warning, message, prompt, and report table cell goes
through `PrintableText`: carriage returns, line feeds, tabs, and the separators U+2028 and U+2029 become spaces, and
every other control character (C0, DEL, and C1) and every bidirectional embedding, override, and isolate (U+202A to
U+202E and U+2066 to U+2069) becomes U+FFFD. A crafted export can therefore neither send escape sequences to the
terminal nor reorder the text around a name. `VaultCommandLine.Invoke` writes System.CommandLine's parse errors, typo
corrections, and help through `PrintableTextWriter`, which keeps line breaks and replaces the same characters, because
System.CommandLine repeats arguments verbatim. Report table cells also escape `|`. Report, brief, and decisions files are
created readable only by their owner.

## Catalog

A catalog lets a person or an agent organize a vault without seeing its secrets. `catalog` writes it from one vault
file, the agent edits only the catalog, and `apply` writes the vault with the edits.

- The catalog holds, per item, its origin key, name, folder, additional folders, favorite, website addresses, notes,
  and text custom fields, plus read-only information: type, user name, which secrets are present, protected field
  names, and attachment names and sizes. It is plain JSON, owner-only on Unix, and read strictly: an unknown, missing,
  or repeated property fails.
- Redaction follows `ShownValues`, which the brief shares: a website address with user info, a query, or a fragment is
  masked and can only be moved. The authority ends at the first `/`, `\`, `?`, or `#`, as browsers read it, and an
  address with an `@` after that is masked after its scheme, because its user info may hold one of those characters.
  A text custom field or user name in which the scanner finds a secret is withheld; hidden, boolean, and linked fields
  show only their names. Passwords, TOTPs, passkeys, password history, and card, identity, and SSH key values are never
  shown.
- Notes are withheld until the user decides on them in the decisions file's `notes` list, by editing it or in
  `review` of the same file: `Share` shows them, `Private` keeps them out, and `Hidden` also moves them into a hidden
  field named `Notes` on `apply`. `catalog` rebuilds the pending entries, one for each item whose notes have no
  decision, with the number of findings in them. The scanner can miss a secret, so no notes are shown without a decision. A
  `Share` decision is bound to the SHA-256 of the notes it shares, recorded by `review` or, for a decision typed into
  the file, by the first `catalog` run; notes that changed since go back to pending. The hash is of notes the catalog
  already shows, so it reveals nothing new.
- `apply` accepts a catalog only for the file it was made from, by file name and SHA-256. It reads the item set from
  the vault and refuses a catalog that adds, removes, or renames a key, empties a name, sets the text of notes that
  the decisions file does not share, or changes or removes a masked address. Field order is kept: shown fields fill
  the places of the shown fields as read, and protected fields keep theirs.
- The trust boundary is the vault password: only the user can run `catalog` and `apply` on a password-protected
  vault. An agent that can write the decisions file can share notes, so the commands print how many notes they show.

## Brief

`merge --brief` writes what an agent needs to propose decisions, without secret values. The merge builds it from the
scanned inputs, the result of the merge with the user's decisions, and those decisions.

- Format: versioned JSON, owner-only on Unix, written through `SecureFile`, and read strictly like the catalog. It
  records each input's file name and SHA-256, the summary counts of the merge, the groups that need a decision, and the
  matcher's suggestions.
- Groups: a group needs a decision when a field of it is unresolved or a member has a finding that is neither
  pre-accepted nor decided. Members are the items as the merge sees them, after accepted findings are placed.
- Fields: the name and the folder when the members hold two or more different values, the single-value fields of the
  merged type with two or more different values, and the lists whose members differ. Each lists its holders, whether
  the policy leaves it to a decision, and whether an agent may take one holder's value: lists take only `Empty`
  decisions, which drop values, so an agent may not decide them.
- Redaction: a value appears only for names, folders, user names, website addresses, and notes. Placement can fill a
  user name or addresses from a finding, such as a connection string, so user names and addresses appear only as the
  inputs hold them, through `ShownValues`, and an item that placement creates shows none. Hosts come from the masked
  addresses. Notes appear only when a `Share` decision matches the notes as read and no finding lies in them: a finding
  that is pending, rejected, or that placement could not move stays in the text. Every other field shows only which
  members hold a value.
- Passwords: `PasswordHealthReport.CheckMembers` checks each member in process: weak as the report defines it, reused
  when a member of another group on other hosts has the same password, and in another member's password history. The
  brief holds these flags only, never a value, length, or hash.
- Newest: a member is newest when every member has a modification time and it alone has the latest. A holder is newest
  when it holds the value the newest policy takes: every holder has a time, and that value's newest holder is newer
  than every other value's.
- Findings: ID, kind, confidence, field path, the 1-based line where the finding starts in its field as read, the
  proposed target, whether it is pre-accepted or needs a decision, and whether an agent may accept or reject it. An
  agent decides without seeing the value, so it may not accept a finding that needs review, that moves text into a
  place catalogs and briefs show (user name, addresses, text fields), or that changes matching, which uses hosts, user
  names, titles, and types and runs after placement; and it may not reject a pre-accepted finding that changes
  matching. Findings appear under the member whose origin key the finding decisions use, never under items that
  placement created.

## Review UI

`review` is the interactive alternative to editing pending entries. It is built with Terminal.Gui 2.5.0, through the
instance model of the [command-line tool guidance](../command-line-tools.md#terminal-uis).

- **Start.** Standard input and standard output must be terminals; otherwise `review` exits with code 3 before it
  reads any input and points to `merge --decisions`. It then runs the checks of `merge`: the output and decisions paths
  through the shared output checks, the input formats, and the password prompts of protected inputs, which it asks at
  the console before the UI starts. Warnings go to standard error before the UI takes over the screen.
- **State.** `ReviewSession` holds the state and has no Terminal.Gui types. It reads and scans the vault once, keeps
  the current decisions, and merges again without output after every change, with the `newest` policy. Pending
  entries of the decisions file count as no decision. It lists groups by status (pending, conflict, suggested,
  identical, unique, has findings) and by a search over item names, and shows each field with every member's value
  and the current decision. Its operations take a member's value for one field or for every conflicting field, drop a
  field, accept or reject a finding, link two items, split an item out of its group, and reveal or mask a value.
- **Masking.** Passwords, TOTP keys, password history, hidden custom fields, card numbers and security codes, SSH
  private keys, identity social security, passport, and license numbers, the user info, query, and fragment of
  website addresses, finding values, and every field of a member that holds one of its findings, such as notes, a user
  name, or a custom field, are masked until revealed. Revealed values reach only the screen.
- **Save and write.** Saving writes the decisions file through `DecisionsFile`, readable only by its owner, with
  pending entries for what is left, so `merge` continues from it. Writing goes through `VaultPipeline.Merge` with an
  output, only when no field or proposed finding is pending and the writer reports no error, never over an input, and
  over an existing file only with `--force`. The password of an encrypted output is asked inside the UI, twice, in
  masked fields, with at least `PasswordReader.MinimumNewPasswordLength` characters.
- **Summary.** `review` writes no report. After the UI, it prints the decisions it saved, the vault it wrote, the
  pending counts, and the number of attachments to upload after the import, with each attachment listed once it wrote
  the vault. The count and the list include the files of Bitwarden attachment folders that match no item or several
  items, each with the number of items its folder matches. Write warnings and the placement problems of accepted
  findings go to standard error. They come from the merge that wrote the vault, or from the latest merge when it wrote
  none. The findings tab shows an accepted finding as applied only when placement moved it, and shows its placement
  problem otherwise.
- **Views.** The window only shows the session and forwards keys: groups on the left, tabs with the field diff and the
  findings on the right, and a status bar with the main keys. `F1` lists every key, and every action has one. Errors
  show value-free messages, and quitting with unsaved decisions asks first. Terminal.Gui 2.5.0 throws while it lays
  out a dialog on a terminal narrower than a few columns, such as one that reports 0x0, so `ReviewDialogs` reports its
  dialogs unavailable below `ReviewDialogs.MinimumWidth` by `ReviewDialogs.MinimumHeight` (60 by 15). The window then
  shows the message on its message line instead, and quitting with unsaved decisions keeps the review open. Names and values show on one line through
  `ReviewText`: each line break that `string.ReplaceLineEndings` recognizes, form feed and NEL included, becomes
  ` ↵ `, and the rest goes through `PrintableText`.
- **Cancellation.** Ctrl+C or SIGTERM before the UI starts, during the checks, the password prompts, or reading the
  inputs, ends the command at once with exit code 1. Inside the UI, Ctrl+C is a key, not a signal. SIGTERM or SIGINT
  cancels the command's token; the window then asks every running dialog to stop, top first, and stops itself without
  asking about unsaved decisions, so the UI restores the terminal and the command exits with code 1. Merging and writing
  run on the UI thread, so a signal during that work takes effect when it ends. When that takes longer than
  System.CommandLine's two-second `ProcessTerminationTimeout`, the process ends with exit code 130 or 143 without
  restoring the terminal.
- **Tests.** `ReviewSession` has unit tests for every operation and for the save and write rules. View tests run
  headless with `Application.Create(new VirtualTimeProvider())`, the ANSI driver, and injected keys, and assert on the
  session and on the dialogs asked. They set `DisableRealDriverIO=1`, because Terminal.Gui otherwise attaches to the
  controlling terminal through `/dev/tty` even when standard input and output are redirected, and they replace the
  driver's clipboard with `FakeClipboard`, because the driver otherwise uses the system clipboard. They share one test
  collection, so a test process never runs two Terminal.Gui applications at once. Cancellation tests run the window
  through the same entry point as the command, with the real dialogs, and cancel while each kind of dialog is open.

## Verification

- Every behavior gets synthetic fixtures and tests. Real exports, tokens, keys, and recovery codes never enter
  the repository. Checksum-valid tokens and private keys are assembled at test runtime, so the public repository
  does not trip secret scanners.
- An independent Node implementation generated the password-protected fixtures, which cross-checks the crypto.
- Interoperability checks are manual and use dummy data: export from a throwaway Vaultwarden account, then
  import the tool's output into a throwaway account.
