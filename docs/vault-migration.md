# Vault migration: Keeper and Bitwarden into Vaultwarden

This guide is for you if you started moving from Keeper to Bitwarden, kept using both vaults, and want one
complete vault on a self-hosted [Vaultwarden](https://github.com/dani-garcia/vaultwarden) server. You export both
vaults, merge the exports offline with `sharedkernel-vault`, and import the result into an empty Vaultwarden
vault.

The tool is under development. Read the status in the
[tool README](../tools/SharedKernel.VaultManager.Tool/README.md) before you use it on real vault data. The
[vault manager plan](plans/vault-manager.md) describes the design. Commands are for bash or zsh on Linux or
macOS. Statements about Keeper, Bitwarden, and Vaultwarden link to their sources.
Anything marked unverified could not be confirmed in those sources.

## How the merge treats your data

- The tool never contacts Keeper, Bitwarden, or Vaultwarden. You create the exports and import the result.
- Items that match across the exports merge one field at a time. A value that only one vault holds is kept.
  When both vaults hold different values, the newest value wins by default. The older password goes to the
  password history. Other older values become custom fields named `<field> (from <input file name>)`, and an
  older note too long for a field is added to the notes.
- Exports carry no deletion markers, so something you deleted in one vault comes back from the other. See
  [Deleted items and values](#deleted-items-and-values).
- Vaultwarden adds every imported item as a new item, so the merged export goes into an empty vault.
- Reports and the decisions file hold no secret values. They do hold item names, folder names, attachment file
  names, and origin keys, and they can name the websites of your passkeys. An origin key holds the record or item
  ID of your export, or, for an item without one, such as a CSV row, a hash of its type, title, and first host.
- The tool reads passwords only from a terminal prompt that does not echo, never from arguments or environment
  variables. Wait for the prompt before you type a password: the terminal shows what you type earlier. Without a
  terminal, a command that needs a password stops with exit code 3 before it does any work and names the input or
  output that needs the password.
- Every argument is a file name or an option. An argument that starts with `@`, such as `@bitwarden.json`, names a
  file called `@bitwarden.json`; the tool reads no response files, so it never splits an export into arguments.
- The tool reads a zip export only within the size limits in [step 4](#4-inspect-the-exports).

## 1. Prepare a working folder

1. Create a folder on an encrypted disk and work in it. Several files in this guide hold your passwords, and
   some of them are not encrypted. A deleted file can stay readable on the disk; disk encryption keeps it
   unreadable.

   ```bash
   mkdir -m 700 ~/vault-merge
   cd ~/vault-merge
   ```

2. Keep the folder out of every backup and sync service. Backup tools usually copy your home folder: Déjà Dup
   backs up "your entire home folder (minus the Trash and Downloads folders)" and skips any folder that holds a
   `.deja-dup-ignore` file ([GNOME](https://help.gnome.org/deja-dup/prefs.html)). If you use Déjà Dup, run:

   ```bash
   touch ~/vault-merge/.deja-dup-ignore
   ```

   On macOS, add the folder to the excluded items in **System Settings** > **General** > **Time Machine** >
   **Options**
   ([Apple](https://support.apple.com/guide/mac-help/exclude-files-from-a-time-machine-backup-mh15622/mac)).
   Exclude it in any other backup or sync tool that you use.

3. Set your browser to ask where to save each download, for example with Chrome's **Ask where to save each file
   before downloading** ([Google](https://support.google.com/chrome/answer/95759)). Save the Bitwarden exports of
   steps 3, 6, and 9 straight into this folder, not into your downloads folder.

4. Turn off core dumps:

   ```bash
   ulimit -c 0
   ```

   This asks the system not to write a core file, which can hold vault data, when a program that you start from
   this shell crashes. The setting covers "the shell and to processes it starts"
   ([Bash manual](https://www.gnu.org/software/bash/manual/html_node/Bash-Builtins.html)), so run it again in
   every new shell that you use for this guide.

5. Stop editing both vaults until you have verified the result. A change you make after an export is not part of
   the merge.

6. Install the .NET SDK version named in the repository's `global.json`, as the
   [development guide](development.md) describes. Clone the repository, build the tool, and define the command
   for this shell:

   ```bash
   git clone https://github.com/danigutsch/PandaBear.SharedKernel.git ~/src/PandaBear.SharedKernel
   cd ~/src/PandaBear.SharedKernel
   dotnet build tools/SharedKernel.VaultManager.Tool/SharedKernel.VaultManager.Tool.csproj -c Release
   bin=~/src/PandaBear.SharedKernel/tools/SharedKernel.VaultManager.Tool/bin/Release/net10.0
   alias sharedkernel-vault="$bin/SharedKernel.VaultManager.Tool"
   cd ~/vault-merge
   sharedkernel-vault --help
   ```

   If you installed the tool package instead, `sharedkernel-vault` is on your path and you skip the alias. In
   every new shell, define the alias again and run `ulimit -c 0`.

## 2. Export Keeper with Commander

Export Keeper with
[Keeper Commander](https://docs.keeper.io/en/keeperpam/commander-cli/command-reference/import-and-export-commands),
not with the web vault. In the web vault, "the encrypted KeePass format (.kdbx) is the only format which
includes attachments saved in records" ([Keeper](https://docs.keeper.io/user-guides/web-vault)), and this tool
does not read KeePass files. Commander's JSON export holds records with their record types and custom fields,
folders, and shared folders with their permissions. It writes each record's modification time as
`last_modified`, which the default merge policy uses. Only with `--zip` does it list each record's attachments
and include the files; without `--zip`, the export lists no attachments at all
([Commander source](https://github.com/Keeper-Security/Commander/blob/v18.1.8/keepercommander/importer/json/json.py)).

1. Install Commander. Keeper offers
   [installers and a developer setup](https://docs.keeper.io/en/keeperpam/commander-cli/commander-installation-setup),
   and publishes the [`keepercommander` package](https://pypi.org/project/keepercommander/). This guide describes
   Commander 18.1.8, so install that version in a Python virtual environment:

   ```bash
   python3 -m venv ~/keeper-env
   source ~/keeper-env/bin/activate
   pip install 'keepercommander==18.1.8'
   ```

   Commander receives your master password and your whole vault. pip also installs dozens of packages that
   Commander depends on, in versions that this pin does not fix, and they run with the same access
   ([PyPI](https://pypi.org/project/keepercommander/18.1.8/)). Install them only on a computer that you trust.
   Keeper also publishes installers for macOS and Windows with SHA-256 checksums
   ([Commander releases](https://github.com/Keeper-Security/Commander/releases/tag/v18.1.8)).

2. Start Commander from the working folder. It writes the zip to the folder you start it from.

   ```bash
   cd ~/vault-merge
   keeper shell
   ```

3. Log in with `login you@example.com`. If your account is in Keeper's EU data center, run `server EU` first.
   Commander then asks for your master password and, when your account needs them, a two-factor code or a device
   approval ([Keeper](https://docs.keeper.io/en/keeperpam/commander-cli/commander-installation-setup/logging-in)).

4. Export:

   ```text
   export keeper.json --format json --zip
   ```

   Commander keeps only the base name of the file and writes `keeper.zip` to the current folder. The zip holds
   `export.json` and one `files/<file UID>` entry per attachment, and it is not encrypted
   ([Commander source](https://github.com/Keeper-Security/Commander/blob/v18.1.8/keepercommander/importer/json/json.py)).
   Commander's help describes `--max-size` as the attachment size limit, 10M by default, but in Commander 18.1.8
   only the KeePass export applies it
   ([KeePass export](https://github.com/Keeper-Security/Commander/blob/v18.1.8/keepercommander/importer/keepass/keepass.py)):
   the JSON export includes every attachment, whatever its size. Add `--owned-only` to leave out records that
   other people share with you
   ([Commander source](https://github.com/Keeper-Security/Commander/blob/v18.1.8/keepercommander/importer/commands.py)).
   On enterprise accounts, a Keeper administrator can block exports
   ([Keeper](https://docs.keeper.io/en/user-guides/export-and-reports/vault-export)).

   The tool reads `keeper.zip` only within the limits in [step 4](#4-inspect-the-exports): at most 1000 entries
   and 100 MiB of expanded files. If the zip breaks a limit, you also need an export without attachments, as
   [When a zip breaks a limit](#when-a-zip-breaks-a-limit) describes.

5. Run `logout` ([Keeper](https://docs.keeper.io/en/keeperpam/commander-cli/command-reference/misc-commands)),
   leave the shell with `q`
   ([Commander source](https://github.com/Keeper-Security/Commander/blob/v18.1.8/keepercommander/cli.py)), and run
   `deactivate`.

## 3. Export Bitwarden

1. Restore anything you want to keep from the Bitwarden trash: "No export formats include trash items"
   ([Bitwarden](https://bitwarden.com/help/export-your-data/)).
2. In the Bitwarden web app, open **Tools** > **Export**. Some web app versions show **Export** under
   **Settings**; the page address ends in `#/tools/export`
   ([Bitwarden clients](https://github.com/bitwarden/clients/blob/main/apps/web/src/app/layouts/user-layout.component.html)).
3. Set **Export from** to **My vault** and pick a **File format**
   ([Bitwarden](https://bitwarden.com/help/export-your-data/)):
   - `.json (Encrypted)` with the **Export type** **Password protected**, when the vault has no attachments. The
     export type starts at **Account restricted**
     ([Bitwarden clients](https://github.com/bitwarden/clients/blob/main/libs/tools/export-vault-ui/src/components/export.component.ts)),
     so change it. Choose a file password; the tool asks for it. A Bitwarden Free vault has no attachments:
     "File attachments are available for Premium users and members of paid organizations"
     ([Bitwarden](https://bitwarden.com/help/attachments/)).
   - `.zip (with attachments)`, when the vault has attachments. The zip holds an unencrypted JSON file and your
     files. Bitwarden offers it for **My vault** only. It puts the files of each item in a folder named after
     the item, so items that share a name get folders such as `Bank` and `Bank_1`
     ([Bitwarden clients](https://github.com/bitwarden/clients/blob/main/libs/tools/export-vault-core/src/services/individual-vault-export.service.ts)),
     and the tool cannot tell which of those items a file belongs to. Step 4 tells you what to do about it. The
     tool reads the zip only within the limits in [step 4](#4-inspect-the-exports).

   Do not use `.csv`: only JSON exports include cards, identities, passkeys, and SSH keys. Do not use the
   **Account restricted** export type: such a file can only be imported into the account that created it, because
   the account's encryption key protects it ([Bitwarden](https://bitwarden.com/help/encrypted-export/)). The tool
   cannot read it offline and stops with exit code 3.
4. Select **Export** and confirm with your master password or an email verification code. If the web app says
   that "corrupted attachments were skipped during export"
   ([Bitwarden clients](https://github.com/bitwarden/clients/blob/main/apps/web/src/locales/en/messages.json)),
   those files are not in the zip, and the merge cannot list them.
5. Save the file in the working folder as `bitwarden.json`, or `bitwarden.zip` for a zip. Decisions name items by
   input file name, so an export of the same vault that you make later needs the same name.

An individual export leaves out items that an organization owns
([Bitwarden](https://bitwarden.com/help/export-your-data/)). They stay in the organization and are not part of
this merge. If you exported a zip, write `bitwarden.zip` wherever this guide writes `bitwarden.json`.

## 4. Inspect the exports

1. Check each zip against the tool's limits. An input that breaks a limit stops `inspect` and `merge` with exit
   code 3 and a message that names the limit:
   - An input file holds at most 256 MiB.
   - A zip holds at most 1000 entries, folders included, and at most 100 MiB (104,857,600 bytes) when expanded.
   - The tool also opens zip files that are attached to items. Their entries count toward both totals, each file
     in them that is larger than 64 KiB may expand to at most 100 times its compressed size, and a zip inside
     them may not hold another zip.

   ```bash
   unzip -l keeper.zip | tail -n 1
   unzip -l bitwarden.zip | tail -n 1
   ```

   The last line of `unzip -l` shows the expanded size in bytes and the number of entries. If a zip breaks a
   limit, follow [When a zip breaks a limit](#when-a-zip-breaks-a-limit).

2. Inspect the exports:

   ```bash
   sharedkernel-vault inspect keeper.zip bitwarden.json
   ```

   For a password-protected export, the tool asks for its password. For each input it prints the detected
   format, item counts by type, folders and how many hold no item, how many items have a modification time, attachments that the export
   includes or leaves out, and read warnings. It prints no values. Check:

   - that each detected format matches what you exported;
   - that the item counts look right for each vault;
   - how many items have no modification time, because the default policy leaves each conflict on such an item
     for you to decide;
   - which attachments an export leaves out. Download those files from their vault before you close it;
   - every warning. Two kinds need you to act:
     - `Attachment folder "<name>" matches 2 items; its files were not attached.`, or `matches no item`, for a
       Bitwarden zip. The tool attaches a folder only when exactly one item can have produced it. The
       **Attachments to upload** section of the report lists the files of such a folder in a second table, with
       their folder, source, and number of matching items, and the summaries of `scan` and `merge` count them.
       After the import, you upload these files by hand (step 8): open the Bitwarden items with that name to see
       which one holds each file. You can instead give each of those items a name that no other item has and export
       again, but editing an item makes it the newest member of its group (step 5), and a new name can stop it
       from matching its Keeper record.
     - `"<item>": attachment "<file>" is listed but not included in the zip.`, for a Keeper zip. The report
       lists the file for you to download from Keeper.
     - `Zip entry "files/<file>" belongs to no record; upload it by hand to the item it belongs to after the
       import.`, for a Keeper zip. No record lists the file, so the merged vault does not name it; keep it with
       the attachments you upload (step 8).

3. To preview secrets stored in the wrong place, such as TOTP seeds or recovery codes in notes, and weak or
   reused passwords, run:

   ```bash
   sharedkernel-vault scan keeper.zip bitwarden.json --report scan-report.md
   ```

   It prints a summary and writes the full report to `scan-report.md`. A password shorter than 12 characters, of
   one character class, or equal to the item's user name or name is listed once per item as weak, without the
   check that failed. Merge runs the same scan, so this step is optional.

### When a zip breaks a limit

For that vault, give merge an export without attachments, and keep the zip as the source of the files:

- Keeper: start Commander and log in again as in step 2, and run `export keeper.json --format json`. Use
  `keeper.json` instead of `keeper.zip` in the `inspect`, `scan`, and `merge` commands of this guide.
- Bitwarden: export `.json (Encrypted)` with the export type **Password protected**, as in step 3, and save it as
  `bitwarden.json`. Use it instead of `bitwarden.zip` in the same commands.

These exports list no attachments, so the merge report lists no file of that vault. Upload every file of that
vault from its zip by hand in step 8. These commands list the files without any secret values: the first prints
each Keeper record title with its file names and the paths of the files in the zip, and the second lists the
Bitwarden files under `attachments/<item name>/`. Install `jq` if you do not have it.

```bash
unzip -p keeper.zip export.json |
  jq -r '.records[] | .title as $title | .attachments[]? | [$title, .name, "files/" + .file_uid] | @tsv'
unzip -l bitwarden.zip
```

## 5. Merge

1. Run merge with a decisions file and a report:

   ```bash
   sharedkernel-vault merge keeper.zip bitwarden.json \
     --output merged.json --decisions decisions.json --report merge-report.md
   ```

   - Exit code 2: some fields or proposed findings need your decision. The tool adds a pending entry for each
     one to `decisions.json` and writes no vault. Continue with the next step.
   - Exit code 0: the tool asks twice for a new password of at least 12 characters and writes `merged.json` as a
     Bitwarden password-protected export. You get three attempts: when the two entries differ or the password is
     shorter, it asks again, and after the third failed attempt it stops with exit code 3 without writing. Use a
     password that protects this file only; you type it again for the import.
   - Exit code 3: an input or output problem, or a value too long for a Bitwarden import, as described below and
     in [Exit codes](#exit-codes). If the output also shows `Wrote the merged vault to merged.json`, the vault is
     complete and only the report could not be written; the error names the reason.

2. Read `merge-report.md`. It lists the groups whose members differ and how each field resolved: `equal`,
   `one-sided`, `union`, `newest`, `preferred source`, `decision`, or `unresolved`. It also lists the secret
   findings with the origin key of each item, password health, the attachments to upload after the import,
   errors, warnings, and the fields and findings that still need a decision.
3. Fill in each pending entry in `decisions.json`, as [The decisions file](#the-decisions-file) describes, or decide
   in the full-screen review and save to the same file, as [The review UI](#the-review-ui) describes.
4. Run the same merge command again. Repeat steps 2 to 4 until merge exits with code 0.

The default policy, `--policy newest`, lets the latest modification time win, and a missing or tied time leaves
the field for you. `--policy ask` leaves every conflict for you. `--policy prefer:bitwarden.json` lets the values
from that input file win conflicts, and uses `newest` for a field that the input holds no value for. The folder is
one of these fields: a folder that only one vault sets is kept, and two different folders go to the policy. The
item name and type are not: they come from the newest member, and each other name is kept in a text field named
`Name (from <input file name>)`.

Merge writes no vault while a value is too long for a Bitwarden import, because "Bitwarden does not process the
import if there is one item invalid"
([Vaultwarden source](https://github.com/dani-garcia/vaultwarden/blob/main/src/api/core/ciphers.rs)). It exits
with code 3 and names each item and field. Shorten the value in its source vault, for example by moving a long
note into a text file that you attach after the import. Export that vault again under the same file name and run
merge again.

Editing an item gives it a new modification time, so it becomes the newest member of its group. With
`--policy newest`, it then wins every conflicting field of that group, and the other vault's value goes to the
password history or to a hidden field even when it is the more recent one. After the next merge, check that
group in the report. For each field that resolves as `newest` but needs the other vault's value, add a `Member`
decision, as [The decisions file](#the-decisions-file) describes.

Once `merged.json` exists, a later run needs `--force` to overwrite it. Merge never overwrites an input file.
`--plaintext` writes an unencrypted export instead; delete that file right after the import.

Merge writes each file to a hidden temporary file in the same folder, such as `.merged.json.<random>.tmp`, and
renames it when the file is complete, so each file is either complete or unchanged. After Ctrl+C, merge exits with
code 1 and starts no further write; a write that it cancels deletes its temporary file. A temporary file remains,
with part of the file in it, only when the process ends during a write: when it is killed, or when a write is still
running 1.5 seconds after Ctrl+C. Delete it.

## 6. Prepare an empty Vaultwarden vault

Vaultwarden creates every imported item as a new item and never updates or deduplicates existing ones
([Vaultwarden source](https://github.com/dani-garcia/vaultwarden/blob/main/src/api/core/ciphers.rs)), and
"Importing does not check for duplicates" ([Bitwarden](https://bitwarden.com/help/import-data/)). Import into an
empty vault, in one of two ways:

- **A new account.** Two-step login, devices, organization memberships, and emergency access need setting up
  again. The account needs an email address that no other account on the server uses
  ([Vaultwarden source](https://github.com/dani-garcia/vaultwarden/blob/main/src/api/core/accounts.rs)).
- **Your existing account, after backups and a purge.** "Purging your vault is permanent. It cannot be undone"
  ([Bitwarden](https://bitwarden.com/help/product-faqs/)), so the backups are required. The vault stays empty
  until the import finishes. Items in the account that neither export holds are lost, unless you export the
  account too and add that file to the merge inputs.

If you have no Vaultwarden account yet, create one. If your Bitwarden side lives in a Vaultwarden account, keep
that account: the purge leaves your two-step login, devices, and organization memberships in place, and the
backups let you recover from a failed import. If signups are closed on your server, the Vaultwarden admin can
still invite a new account
([Vaultwarden source](https://github.com/dani-garcia/vaultwarden/blob/main/src/config.rs)).

For your existing account, purge only after merge has written `merged.json`:

1. If you run the server, back it up as the
   [Vaultwarden backup guide](https://github.com/dani-garcia/vaultwarden/wiki/Backing-up-your-vault) describes. At
   least copy the database and the `attachments` folder from the `data` folder. For the default SQLite database:

   ```bash
   sqlite3 data/db.sqlite3 ".backup '/path/to/backups/db-$(date '+%Y%m%d-%H%M').sqlite3'"
   ```

2. Export the vault as `.zip (with attachments)` from **My vault**, as in step 3, and save it as
   `vaultwarden-backup.zip` in the working folder.
3. Open **Settings** > **My account** (`#/settings/account`), go to **Danger zone**, select **Purge vault**, and
   confirm with your master password ([Bitwarden](https://bitwarden.com/help/product-faqs/)). The purge deletes
   every item and folder of your individual vault, with their attachments. Items that an organization owns stay
   ([Vaultwarden source](https://github.com/dani-garcia/vaultwarden/blob/main/src/db/models/cipher.rs)).

To bring your existing account back to its state before the purge, for example after a failed import:

- **From `vaultwarden-backup.zip`.** If the vault is not empty, purge it again. The **Bitwarden (json)** and
  **Bitwarden (csv)** import formats read JSON and CSV files, not zips
  ([Bitwarden clients](https://github.com/bitwarden/clients/blob/main/libs/importer/src/models/import-options.ts)),
  so extract the JSON file, import `restore.json` as in step 7, without a file password, upload the files from
  the `attachments` folder of the zip as in step 8, and delete `restore.json`:

  ```bash
  unzip -p vaultwarden-backup.zip data.json > restore.json
  ```

- **From the server backup.** Stop Vaultwarden, delete any `db.sqlite3-wal` file in the `data` folder, and copy
  the backed-up database and `attachments` folder back into the `data` folder
  ([Vaultwarden wiki](https://github.com/dani-garcia/vaultwarden/wiki/Backing-up-your-vault)). The database holds
  every account on the server, so this sets every account, not only yours, back to the time of the backup. On a
  server that other people use, restore from `vaultwarden-backup.zip` instead.

## 7. Import the merged export

1. Sign in to your Vaultwarden web vault with the account from step 6.
2. Open **Tools** > **Import**. The page address ends in `#/tools/import`.
3. Set **Vault** to **My vault** and leave **Folder** empty. A folder you pick there becomes the parent of every
   imported folder ([Bitwarden](https://bitwarden.com/help/import-from-keeper/)).
4. Set **File format** to **Bitwarden (json)**
   ([Bitwarden clients](https://github.com/bitwarden/clients/blob/main/libs/importer/src/models/import-options.ts)),
   choose `merged.json`, and select **Import**.
5. Enter the password of `merged.json` in the **Confirm vault import** window
   ([Bitwarden](https://bitwarden.com/help/import-data/)).

When an import is rejected, "No data is added to your vault"
([Bitwarden](https://bitwarden.com/help/import-data/)). Vaultwarden rejects the whole import when one item is
invalid, for example when a note is longer than 10,000 encrypted characters
([validation](https://github.com/dani-garcia/vaultwarden/blob/main/src/db/models/cipher.rs),
[default limit](https://github.com/dani-garcia/vaultwarden/blob/main/src/config.rs)).

Vaultwarden also limits an import request to 20,000,000 bytes, which fits "something like 5000+ vault entries"
by its own estimate: its code sets `20.megabytes()`
([Vaultwarden source](https://github.com/dani-garcia/vaultwarden/blob/main/src/main.rs)), and the `ubyte` crate
counts a megabyte as 1,000,000 bytes ([ubyte](https://docs.rs/ubyte/latest/ubyte/struct.ByteUnit.html)). The web
app encrypts every value before it sends the import, so the request is larger than `merged.json`. Merge estimates
the request as `merged.json` in compact JSON, with each value and folder name as an encrypted string and an
encrypted SHA-256 checksum for each login URI, and warns when that estimate exceeds 20,000,000 bytes. Merge cannot
split the export. The Vaultwarden wiki describes raising the limit
with `ROCKET_LIMITS`
([Vaultwarden wiki](https://github.com/dani-garcia/vaultwarden/wiki/Changing-the-API-request-size-limit)), but
the current code sets its limits after it reads that setting, so whether `ROCKET_LIMITS` raises the import limit
is unverified. A reverse proxy in front of Vaultwarden adds a limit of its own: nginx accepts 1 MB by default
([nginx](https://nginx.org/en/docs/http/ngx_http_core_module.html#client_max_body_size)), and the setting in
step 8 raises it.

Vaultwarden does not take item dates from the file, so every imported item shows the import time
([Vaultwarden source](https://github.com/dani-garcia/vaultwarden/blob/main/src/db/models/cipher.rs)). Keep the
exports until you have verified the vault.

## 8. Upload attachments

A JSON import cannot carry files, so "Upload file attachments to the new vault individually"
([Bitwarden](https://bitwarden.com/help/import-data/)). The **Attachments to upload** table in
`merge-report.md` lists each item, the file name, and where to find the file: an input file and the path inside
it, or a note that the export leaves the file out and you download it from the source vault. A second table lists
the files of the Bitwarden attachment folders that inspect warned about in step 4, with the folder, the file name,
where to find the file, and how many items the folder matches; open the items with that name to find the one each
file belongs to. Neither table lists the files of a vault that you merged from an export without attachments, as
[When a zip breaks a limit](#when-a-zip-breaks-a-limit) describes; upload those from their zip in the same way.

1. Extract each file under its own name. Commander stores files as `files/<file UID>`, without their names:

   ```bash
   unzip -p keeper.zip 'files/<file UID>' > '<file name>'
   ```

   For `bitwarden.zip`, use the path from the report, or from `unzip -l bitwarden.zip`, the same way.
2. In the web vault, open the item, select **Edit**, then **Attachments**, **Choose File**, **Upload**, and
   **Save** ([Bitwarden](https://bitwarden.com/help/attachments/)).
3. Delete the extracted file.

For many files, use the [Bitwarden CLI](https://bitwarden.com/help/cli/). Point it at your server before you log
in, log in and unlock as its documentation describes, and attach each file to its item:

```bash
bw config server https://vault.example.com
bw login
bw list items --search '<item name>' | jq -r '.[] | .id + "  " + .name'
bw create attachment --file '<file name>' --itemid '<item ID>'
bw logout
```

`bw list items` prints whole items, passwords included, so the `jq` filter shows only IDs and names; install
`jq` if you do not have it. The session key that `bw login` and `bw unlock` print unlocks your vault; keep it
private. End your session with `bw logout` ([Bitwarden](https://bitwarden.com/help/cli/)), which also signs the
CLI out of your account; `bw lock` only locks the vault
([Bitwarden clients](https://github.com/bitwarden/clients/blob/main/apps/cli/src/auth/commands/logout.command.ts)).

Bitwarden clients upload files up to 500 MB, or 100 MB from a mobile app
([Bitwarden](https://bitwarden.com/help/attachments/)). Vaultwarden accepts files up to 525 MB
([Vaultwarden source](https://github.com/dani-garcia/vaultwarden/blob/main/src/main.rs)). A reverse proxy in
front of it needs a matching body size limit, such as `client_max_body_size 525M;` for nginx
([Vaultwarden wiki](https://github.com/dani-garcia/vaultwarden/wiki/Proxy-examples)). The server setting
`USER_ATTACHMENT_LIMIT` caps each account's attachment storage in kilobytes
([Vaultwarden](https://github.com/dani-garcia/vaultwarden/blob/main/.env.template)).

## 9. Verify the vault

1. Compare item counts. Export **My vault** as `.json (Encrypted)` with the export type **Password protected**,
   as in step 3, save it as `check.json` in the working folder, and run:

   ```bash
   sharedkernel-vault inspect merged.json check.json
   ```

   Both files show the same item counts per type and the same folders.
2. Check attachments. For every item that has attachments in Keeper or Bitwarden, check that its files are in
   Vaultwarden. The report lists no file of a vault that you merged from an export without attachments (step 8),
   and a Bitwarden zip leaves out corrupted attachments (step 3). These commands list what the
   exports hold and what Vaultwarden holds, without any secret values:

   ```bash
   unzip -p keeper.zip export.json |
     jq -r '.records[] | .title as $title | .attachments[]? | [$title, .name] | @tsv'
   unzip -l bitwarden.zip
   bw config server https://vault.example.com
   bw login
   bw list items | jq -r '.[] | .name as $name | .attachments[]? | [$name, .fileName] | @tsv'
   bw logout
   ```

   A Bitwarden zip changes some characters in item and file names
   ([Bitwarden clients](https://github.com/bitwarden/clients/blob/main/libs/tools/export-vault-core/src/services/individual-vault-export.service.ts)),
   so compare those names by eye.
3. Check TOTP codes. "Generating TOTP codes is available with Premium"
   ([Bitwarden](https://bitwarden.com/help/integrated-authenticator/)), and Vaultwarden reports every account as
   premium ([Vaultwarden source](https://github.com/dani-garcia/vaultwarden/blob/main/src/db/models/user.rs)). For
   each login with a code, compare the code with the one Keeper or your authenticator app shows at the same
   moment, or sign in with it. Start with the items whose TOTP findings the merge report lists as accepted,
   because their seeds came from notes or other fields. Extra seeds of a login are in hidden fields whose names
   start with `TOTP`.
4. Check passkeys. A login with a passkey shows a **Passkey** field with its creation date
   ([Bitwarden](https://bitwarden.com/help/storing-passkeys/)). Sign in once with each passkey from the browser
   extension or a mobile app. Unverified: whether a passkey exported from Keeper signs in from a Bitwarden client,
   and how clients handle a merged login that holds passkeys from both vaults, given that "Only one passkey can be
   saved per login item". Merge warns about each login that holds more than one passkey, and the report lists
   these warnings. To keep each passkey on a login of its own, split that group, as
   [The decisions file](#the-decisions-file) describes. Test them before you close your Keeper account.
5. Check folders. Bitwarden nests a folder only when a folder named after its parent exists, such as `Work` for
   `Work/Projects`; otherwise it shows the full name ([Bitwarden](https://bitwarden.com/help/folders/)). The merged
   vault creates every folder of your exports, including empty ones, and the parent of each. An item holds one
   folder: a Keeper record that is in several folders lands in the first one, a Bitwarden item in several
   collections lands in the first collection, and an item whose merged copies sat in different folders lands in
   the folder the policy chose. Its `Additional folders` field lists the others; move the item if you prefer one of
   them. A `/` inside a Keeper folder name becomes `-`. Keeper shared-folder permissions are not carried over; the
   merge report's conversion notes name the affected items.
6. Check password history. Open a login and select **Password history**. Bitwarden "stores the last five saved
   passwords for each login item"
   ([Bitwarden](https://bitwarden.com/help/password-and-generator-history/)), so merge moves older entries into
   hidden fields named `Previous password (<date>)`. A password that lost a conflict is in the history.
7. Check SSH keys. Vaultwarden shows SSH key items only to clients from version 2024.12.0 on
   ([Vaultwarden source](https://github.com/dani-garcia/vaultwarden/blob/main/src/api/core/ciphers.rs)).
8. Spot-check the groups under **Changed groups** in the merge report, starting with fields resolved as `newest`
   or `decision`.

When the report flags a Bitwarden account recovery code, the tool leaves it where it is. Move it out of the
vault that it protects.

## 10. Clean up

1. Switch your devices to Vaultwarden. Close your Keeper and Bitwarden accounts only after you have verified the
   new vault, attachments included.
2. Delete the exports, reports, and decisions file:

   ```bash
   cd ~/vault-merge
   rm -f keeper.zip keeper.json bitwarden.json bitwarden.zip merged.json check.json restore.json
   rm -f decisions.json all-conflicts.json scan-report.md merge-report.md
   ```

   Keep `vaultwarden-backup.zip` from step 6 only as long as you need it, on the encrypted disk. Bitwarden's
   advice for unencrypted exports applies to every export here: do not send them over insecure channels, "and
   delete the file immediately after use" ([Bitwarden](https://bitwarden.com/help/export-your-data/)). When the
   working folder holds nothing that you still need, delete it with everything in it, extracted attachments
   included:

   ```bash
   cd ~
   rm -rf ~/vault-merge
   ```

3. Remove Commander's login data. After `logout`, Commander keeps its device registration, by default in the
   system keychain under the name `KeeperCommander`
   ([Keeper](https://docs.keeper.io/keeperpam/commander-cli/commander-installation-setup/configuration/configuration),
   [Commander source](https://github.com/Keeper-Security/Commander/blob/v18.1.8/keepercommander/config_storage/os_keychain/os_keychain_storage.py)).
   It keeps its `config.json` in `~/.keeper`, or, when that folder does not exist, in `~/.local/share/.keeper`
   on Linux and in `~/Library/Application Support/.keeper` on macOS
   ([Commander source](https://github.com/Keeper-Security/Commander/blob/v18.1.8/keepercommander/utils.py)).
   Delete the keychain entries named `KeeperCommander`, in Keychain Access on macOS or in the keyring app of your
   Linux desktop, and that `config.json`. Then remove Commander's virtual environment:

   ```bash
   rm -rf ~/keeper-env
   ```

## Deleted items and values

Exports carry no deletion markers, so the tool cannot tell a deletion from an entry that one vault never had.

- A value that you removed in one vault, such as a TOTP seed, comes back from the other vault, because a value
  that only one side holds is always kept. For a single-value field, such as `notes`, `login.password`, or
  `login.totp`, add an `Empty` choice for that field to the item's group in `decisions.json`. The merge report
  gives the group key and shows the field as `one-sided`.
- Custom fields, URIs, passkeys, and password history are combined from all members into one list each, and the
  report shows them as `union`. An `Empty` choice on `fields`, `login.uris`, `login.fido2Credentials`, or
  `passwordHistory` drops the whole list from every member, so it cannot drop one deleted custom field, URI, or
  passkey. Delete such a value in Vaultwarden after the import.
- A whole item that you deleted in one vault comes back from the other. The decisions file cannot drop a whole
  item. Delete it in the other vault before you export, or in Vaultwarden after the import.
- Bitwarden exports leave out the trash, so restore anything you want to keep before you export. An export that
  holds items in the trash anyway gets a warning that counts them, and the tool skips them.
- Unverified: whether Commander exports records from Keeper's deleted items. Delete any that appear after the
  import.

## The decisions file

The decisions file holds group keys, field paths, origin keys, item names, finding IDs, and your choices, never
secret values. Item names appear as typed in your vaults, except that control and format characters appear as
JSON escapes, such as `\u202E` for a right-to-left override, so that they cannot reorder or hide text in your
editor. When decisions are needed, merge creates or updates the file: it adds a pending entry for each field and
each proposed finding that needs your choice, and keeps every other entry as it is. A pending entry counts as no
decision. While a field or a proposed finding has no decision, merge exits with code 2 and writes no vault. The
**Decisions needed** section of the merge report lists the same entries.

Merge reads only the property names that this section shows, each at most once in its object. A property with any
other name, such as `finding` instead of `findings`, stops merge with exit code 3, and the error names the file and
the JSON path of that property, such as `$.finding`. A property given twice, such as a second `findings` list that
you add above the one merge wrote, stops merge in the same way, so add your choices to the existing list. Correct
the file and run merge again; until then, no choice in the file takes effect.

An origin key names one input item as `<provider>|<input file name>|<ID in that export>`, for example
`keeper|keeper.zip|q8Ck2Rb0Tz4mW1xV6yN3aA`. Items without an ID, such as CSV rows, get a key from a hash of their
type, title, and first host instead. A group key names a set of matched items. After a merge that
needs two decisions, the file holds:

```json
{
  "version": 2,
  "groups": [
    {
      "groupKey": "0000000000000000",
      "choices": [
        {
          "fieldPath": "login.password",
          "kind": "Pending",
          "options": [
            {
              "originKey": "keeper|keeper.zip|q8Ck2Rb0Tz4mW1xV6yN3aA",
              "itemName": "Example Bank"
            },
            {
              "originKey": "bitwarden|bitwarden.json|3f2504e0-4f89-11d3-9a0c-0305e82c3301",
              "itemName": "Example Bank"
            }
          ]
        }
      ]
    }
  ],
  "links": [],
  "splits": [],
  "findings": [
    {
      "originKey": "bitwarden|bitwarden.json|3f2504e0-4f89-11d3-9a0c-0305e82c3301",
      "choices": [
        {
          "findingId": "notes-recovery-code@notes:42",
          "decision": "Pending"
        }
      ],
      "itemName": "Example Bank"
    }
  ]
}
```

To decide, change each pending entry. After these changes, the group keeps the Keeper password and drops the
TOTP seed from every member, and the finding is accepted. The `login.totp` choice is one you add yourself, for a
seed that you deleted in one vault:

```json
{
  "version": 2,
  "groups": [
    {
      "groupKey": "0000000000000000",
      "choices": [
        {
          "fieldPath": "login.password",
          "kind": "Member",
          "originKey": "keeper|keeper.zip|q8Ck2Rb0Tz4mW1xV6yN3aA"
        },
        {
          "fieldPath": "login.totp",
          "kind": "Empty"
        }
      ]
    }
  ],
  "links": [],
  "splits": [],
  "findings": [
    {
      "originKey": "bitwarden|bitwarden.json|3f2504e0-4f89-11d3-9a0c-0305e82c3301",
      "choices": [
        {
          "findingId": "notes-recovery-code@notes:42",
          "decision": "Accepted"
        }
      ],
      "itemName": "Example Bank"
    }
  ]
}
```

- A field choice has `"kind": "Member"` and the `originKey` of the item whose value you keep, taken from the
  `options` of the pending entry, or `"kind": "Empty"` to drop the field from every member. Merge ignores
  `options`, so you can leave it in place. `Member` works for `name`, `folder`, and single-value fields, and
  `Empty` also works for `fields`, `passwordHistory`, `login.uris`, and `login.fido2Credentials`, where it drops
  the whole list (see [Deleted items and values](#deleted-items-and-values)). Field paths follow Bitwarden export
  property names, such as `name`, `notes`, `login.username`, `login.password`, `login.totp`, and `card.number`.
- A finding choice has `"decision": "Accepted"`, which moves the secret to its proposed field, or `"Rejected"`,
  which leaves it where it is. Pre-accepted findings, which have high confidence and need no review, apply
  unless you reject them, so they get no pending entry: to reject one, add a `Rejected` choice with its finding
  ID under its item's origin key, both from the secret findings table of the report, to the `findings` list.
  Other findings apply only when you accept them.
- To decide a field that has no pending entry, such as a value to drop or a value to keep after an edit
  (step 5), add a choice to the group's `choices`, or add a group `{ "groupKey": "<group key>", "choices": [] }`
  with the choice in its list. Pending entries list the origin keys of a group's members. To list them for every
  conflicting field, run the merge once with `--policy ask` and a separate decisions file, read the `options` in
  `all-conflicts.json`, and delete that file. If this run asks for a new password, no field needs a decision;
  press Ctrl+C.

  ```bash
  sharedkernel-vault merge keeper.zip bitwarden.json \
    --output unused.json --decisions all-conflicts.json --policy ask
  ```

- Merge warns about each decision whose group, field, origin key, or finding ID matches nothing. A `Member` choice
  whose `originKey` is not one of the group's members counts as no decision: the policy decides the field, and
  when the field needs your decision, merge puts its pending entry, with the members as `options`, in the choice's
  place.
- `links` and `splits` correct the matching. Each entry is `{ "first": "<origin key>", "second": "<origin key>" }`.
  A link puts two items into one group, and a split keeps them apart and wins over a link. A link or a split
  changes the key of the group, so check the decisions for that group again after the next run.

## The review UI

`sharedkernel-vault review` is the interactive alternative to editing the decisions file: a full-screen terminal UI
that shows each group of matched items side by side, with its findings, and saves your choices to the same decisions
file. Run it in a terminal with the inputs of your merge:

```bash
sharedkernel-vault review keeper.zip bitwarden.json --decisions decisions.json --output merged.json
```

Before the UI opens, the review checks the files as merge does and asks for the password of each password-protected
input. It needs a terminal: when its input or output is redirected, it stops with exit code 3, and you edit the
decisions file as [The decisions file](#the-decisions-file) describes instead. The review resolves conflicts with the
default `newest` policy. Its dialogs need a terminal of at least 60 columns by 15 rows; on a smaller one, the review
shows their messages on its message line and stays open when you quit with unsaved decisions, so save with `Ctrl+S`
or enlarge the terminal.

1. Press `F7` until the groups list shows the `pending` groups, the groups with a field or a finding that needs your
   decision. `Ctrl+F` searches the groups by item name.
2. Select a group and press `F3` for its fields. Each item of the group has a column; move to the column of the item
   whose value you keep. `Enter` takes that value for the selected field, `a` takes it for every field whose items hold
   different values, `e` drops the field from every item, and `Delete` removes your decision. Passwords, TOTP keys,
   and other secrets show as `********`; `v` reveals and masks the selected field.
3. Press `F4` for the findings of the group. `a` accepts a finding and moves the secret to its proposed place, `r`
   rejects it and leaves it where it is, and `v` reveals and masks its value.
4. To correct the matching, mark a group with `m` in the groups list, select another group, and press `l` to link
   them. In the fields, `s` splits the item of the selected column out of its group.
5. Press `Ctrl+S` to save the decisions file. A merge with the same decisions file continues from it.
6. When nothing needs a decision, `Ctrl+W` asks twice for the password of the new export, at least 12 characters, and
   writes `merged.json` as merge does. Without `--output`, the review saves decisions only. A JSON import cannot carry
   files, so after you quit, the review lists each attachment to upload and where to find it, as the **Attachments to
   upload** tables of the merge report do, including the files of attachment folders that match no item or several
   items. Use that list in [8. Upload attachments](#8-upload-attachments).
7. `Ctrl+Q` or `Esc` quits; `Ctrl+C` does not. With unsaved decisions, the review asks whether to save them first. `F1`
   lists every key.

Revealed values appear only on the screen. The decisions file, the messages, and what the review prints after you
quit hold no secret values.

## Exit codes

- `0`: success.
- `1`: an invalid command line, a cancellation, or an unexpected error. Ctrl+C or SIGTERM cancels a command, also
  while the review checks the files or asks for passwords before its UI opens. Inside the review UI, Ctrl+C is a key
  and does not end the review.
- `2`: merge needs decisions and writes no vault. With `--decisions <file>`, the file holds pending entries for
  you to fill in. Without it, run merge again with `--decisions <file>`.
- `3`: an input or output problem. An input is unreadable, unsupported, account-restricted, or over a size limit, or
  its password is wrong. A Bitwarden JSON export is malformed, for example because it holds a null item or two
  folders with one ID; the message names the first malformed entry by its position. The decisions file is not valid, for example
  because a property name is misspelled or a property appears twice in one object. The output exists and `--force`
  is missing, the output is also an input or differs from one only in case, or its folder does not exist. The report
  path names an existing file that is not a vault report. The report or the decisions file cannot be written. A value would make Bitwarden reject the import; merge still adds pending
  entries for the decisions it needs. A password is needed and no terminal is available, or three attempts at a new
  password fail. Merge writes no vault in these cases, except when it writes `merged.json` and then cannot write the
  report: it prints `Wrote the merged vault to merged.json` before it writes the report, and the error says that the
  vault is written and the report is not.
- `130` or `143`: Ctrl+C or SIGTERM arrived while the tool was starting, or the canceled command did not end
  within two seconds.
